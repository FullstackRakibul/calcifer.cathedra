# Calcifer.SweetPDF — PDF to JSON Extraction Module

A Cathedra module that accepts a PDF upload and returns structured JSON: document metadata,
per-page text, word bounding boxes, and optionally detected fields (emails, phone numbers, dates,
invoice numbers, monetary amounts). Built on [PdfPig](https://github.com/UglyToad/PdfPig).

Like every Cathedra module, it gets the platform envelope (`ApiResponse<T>`), correlation-id
logging, and the global exception handler for free from the kernel.

## Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/v1/sweetpdf/extract` | Full extraction: metadata + pages + words + structured data. |
| POST | `/api/v1/sweetpdf/extract-text` | Lightweight: the document's text only. |

Both take `multipart/form-data` with the PDF in the **`file`** field:

```bash
curl -X POST http://localhost:<port>/api/v1/sweetpdf/extract -F "file=@invoice.pdf"
```

Failures come back as the standard envelope with a mapped 4xx status: `FILE_REQUIRED`,
`FILE_TOO_LARGE`, `INVALID_FILE_TYPE`, `PDF_ENCRYPTED`, `PDF_EXTRACTION_FAILED`.

## Layout

```
SweetPdfModule.cs                 IModule: service registration, endpoint mapping, lifecycle
Endpoints/SweetPdfEndpoints.cs    upload validation + Result -> ApiResults.From
Models/                           ExtractResponse, PageContent, WordDto, PdfMetadataDto
Services/                         IPdfExtractionService (PdfPig pipeline), IPdfTypeDetector
Parsers/                          ITextStructureParser (regex field detection, 2s timeout each)
Infrastructure/Configuration/     PdfExtractionOptions (bound from Cathedra:PdfExtraction)
```

## Configuration (`appsettings.json`, all optional)

```json
"Cathedra": {
  "PdfExtraction": {
    "MaxFileSizeBytes": 26214400,
    "IncludeWords": true,
    "DetectStructure": true,
    "Passwords": [],
    "MaxPages": 0
  }
}
```

## Design notes / deviations from the original plan

- **Services return `Result<T>`** and endpoints translate via `ApiResults.From`, following the
  kernel convention — instead of try/catch + raw `Results.BadRequest` in handlers. Corrupt or
  password-protected files become clean enveloped 400s, not 500s.
- **The upload is buffered to a `MemoryStream`** before parsing: PdfPig requires a seekable stream
  (it reads the trailer first) and the multipart body stream is forward-only. Safe because uploads
  are size-capped first.
- **`IPdfTypeDetector` operates on the already-open `PdfDocument`** rather than re-opening the
  stream, so each upload is parsed exactly once. Scanned detection = fewer than ~10 letters on
  every page; scanned PDFs still return metadata with empty text (OCR is a future concern).
- **Package version**: the NuGet feed offers no stable PdfPig; `1.7.0-custom-5` is pinned because
  it is the only internally consistent set (the `0.1.9-alpha001-patch1` metapackage references
  sub-assemblies that the feed does not have, and fails to compile). This build exposes the core
  0.1.8-era API, so text uses `page.Text` and words use `page.GetWords()` (the layout-analysis
  `ContentOrderTextExtractor` namespace is not present in it).
- The planned `Processors/` (digital/scanned split) and `PdfPigExtensions` files were dropped:
  with a single processor and no OCR yet they would be dead code. The seam for OCR is
  `IPdfTypeDetector` + a branch in `PdfExtractionService.Extract`.
- Metadata dates are returned as **raw PDF date strings** (e.g. `D:20240115120000Z`) because the
  format in the wild is not reliably parseable to `DateTime`.
