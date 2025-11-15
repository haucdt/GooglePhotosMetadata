# Google Photos Metadata Restoration Utility

**Version:** 1.0  
**Author:** [haucdt]  


---

## Overview

**Google Photos Metadata Restoration Utility** is a desktop tool designed to restore original metadata for photos and videos exported from Google Photos via Google Takeout. It reads JSON sidecar files provided by Google Takeout and writes timestamps, GPS information, and descriptions back into the original media files using ExifTool.

This ensures that photos and videos retain their original shooting dates and locations, enabling correct chronological organization in photo management software.

---

## Features

- **JSON Sidecar Parsing:** Supports `photoTakenTime`, `videoTakenTime`, `creationTime`, and additional metadata fields.
- **Timestamp Restoration:** Writes `DateTimeOriginal`, `CreateDate`, `ModifyDate` for images and videos. For videos, QuickTime and XMP tags are updated when applicable.
- **GPS Metadata:** Restores GPS coordinates if available in the JSON.
- **Description Restoration:** Writes image or video description from the JSON sidecar.
- **Format-Aware Writing:** Only writes metadata to supported formats (JPEG, HEIC, HEIF, MP4, MOV). Unsupported formats like PNG, GIF, WEBP are skipped with logging.
- **Batch Processing:** Processes multiple files concurrently with safe multi-threading.
- **Recursive Folder Scan:** Automatically finds media files and JSONs in nested directories.
- **Safe ExifTool Pipeline:** Uses temporary argument files for safe Unicode handling and prevents data corruption.
- **Detailed Logging:** Logs all successful operations, errors, skipped files, and mismatched JSON files.

---

## Requirements

- Windows 10 or higher (tested)
- [.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework)
- [ExifTool](https://exiftool.org/) — place `exiftool.exe` in the same folder as the application or ensure it is in the system PATH.

---

## Supported File Formats

| Media Type | Supported? |
|------------|------------|
| JPEG (.jpg, .jpeg) | ✅ Yes |
| HEIC / HEIF | ✅ Yes |
| PNG / GIF / WEBP --- |
| MP4 | ✅ Yes |
| MOV | ✅ Yes |

---

## Usage

1. Launch the application (`GooglePhotosMetadata.exe`).
2. Click **Select Folder** and choose the folder containing your exported Google Photos media and JSON files.
3. Check or uncheck **Recursive Scan** to include subfolders.
4. Click **Process** to start batch metadata restoration.
5. Wait for the progress to finish. The status label will show the number of successful and failed operations.
6. After completion, a log file `GooglePhotosMetadata_Log.txt` is created in the selected folder.

---

## Example





Tiện ích khôi phục metadata Google Photos

Ứng dụng này được thiết kế để tái tạo và ghi lại metadata chính xác cho các file ảnh và video được xuất từ Google Photos thông qua Google Takeout. Công cụ tự động ghép nối từng file ảnh/video với file JSON tương ứng, trích xuất các trường thời gian và metadata, sau đó ghi trực tiếp vào file gốc thông qua ExifTool với quy trình an toàn.

Các chức năng kỹ thuật chính:

Phân tích JSON sidecar: Đọc và kiểm tra các file JSON từ Google Takeout, hỗ trợ đầy đủ các trường như photoTakenTime, creationTime và các metadata mở rộng.

Khôi phục timestamp: Ghi lại DateTimeOriginal, CreateDate, ModifyDate cho các định dạng hỗ trợ (JPEG, HEIC, MP4, MOV...). Với video, metadata được ghi vào cả QuickTime và XMP khi phù hợp.

Xử lý theo định dạng: Chỉ ghi metadata vào các định dạng cho phép. Những định dạng không hỗ trợ EXIF (PNG, GIF...) sẽ được bỏ qua và ghi lại trong log.

Xử lý hàng loạt: Tối ưu tốc độ với đa luồng, đảm bảo an toàn dữ liệu bằng cơ chế đồng bộ hoá.

Quét thư mục đệ quy: Tự động tìm tất cả file media và JSON tương ứng trong toàn bộ cây thư mục.

Pipeline ghi an toàn: Sử dụng file lệnh tạm cho ExifTool, kiểm tra mã thoát của tiến trình để tránh lỗi ghi hỏng file.

Ghi log & xử lý lỗi: Ghi chi tiết tất cả thao tác, file bỏ qua, lỗi, và các trường hợp không tương thích nhằm hỗ trợ kiểm tra và xử lý.

Công cụ đảm bảo độ tin cậy cao trong việc khôi phục metadata khi trích xuất từ Google Photos, giúp hệ thống file media được sắp xếp lại chính xác theo thời gian trên mọi hệ điều hành và phần mềm quản lý ảnh.
