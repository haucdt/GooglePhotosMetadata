using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace GooglePhotosMetadata
{
    public partial class Form1 : Form
    {
        private string selectedFolder = "";
        private ConcurrentBag<string> successList = new ConcurrentBag<string>();
        private ConcurrentBag<string> errorList = new ConcurrentBag<string>();
        private bool recursiveScan = true;
        private long processedCount = 0;
        private long totalCount = 0;

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            chkRecursive.Checked = true;
            lblStatus.Text = "Sẵn sàng. Chọn thư mục để bắt đầu.";
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục chứa ảnh + JSON (sẽ quét tất cả thư mục con nếu bật đệ quy)";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    selectedFolder = fbd.SelectedPath;
                    txtFolder.Text = selectedFolder;
                    lblStatus.Text = $"Sẵn sàng. Đệ quy: {(recursiveScan ? "CÓ" : "KHÔNG")}";
                    lblStatus.ForeColor = System.Drawing.Color.Blue;
                    btnProcess.Enabled = true;
                }
            }
        }

        private void chkRecursive_CheckedChanged(object sender, EventArgs e)
        {
            recursiveScan = chkRecursive.Checked;
            lblStatus.Text = $"Sẵn sàng. Đệ quy: {(recursiveScan ? "CÓ" : "KHÔNG")}";
        }

        private async void btnProcess_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFolder) || !Directory.Exists(selectedFolder))
            {
                lblStatus.Text = "Lỗi: Chưa chọn thư mục!";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                return;
            }

            if (!File.Exists("exiftool.exe"))
            {
               // lblStatus.Text = "Lỗi: Không tìm thấy exiftool.exe trong thư mục chương trình!";
             //   lblStatus.ForeColor = System.Drawing.Color.Red;
              //  return;
            }

            btnProcess.Enabled = false;
            btnSelectFolder.Enabled = false;
            successList = new ConcurrentBag<string>();
            errorList = new ConcurrentBag<string>();
            processedCount = 0;

            var searchOption = recursiveScan ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = Directory.GetFiles(selectedFolder, "*.*", searchOption)
                .Where(f =>
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".heif", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!imageFiles.Any())
            {
                lblStatus.Text = "Không tìm thấy ảnh/video nào!";
                btnProcess.Enabled = true;
                btnSelectFolder.Enabled = true;
                return;
            }

            totalCount = imageFiles.Count;
            progressBar1.Minimum = 0;
            progressBar1.Maximum = (int)totalCount;
            progressBar1.Value = 0;
            progressBar1.Visible = true;

            lblStatus.Text = $"Đang xử lý 0/{totalCount} file...";
            lblStatus.ForeColor = System.Drawing.Color.Orange;

            await Task.Run(() =>
            {
                Parallel.ForEach(imageFiles, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                    imagePath =>
                    {
                        string finalPath = imagePath;
                        string jsonPath = FindJsonForImage(imagePath);
                        bool success = false;

                        try
                        {
                            if (jsonPath != null)
                            {
                                success = ProcessJsonToImage(jsonPath, ref finalPath);
                                if (success)
                                {
                                    successList.Add(finalPath);
                                    TryDeleteJson(jsonPath);
                                }
                                else
                                {
                                    errorList.Add($"{Path.GetFileName(finalPath)} | Không ghi được metadata");
                                }
                            }
                            else
                            {
                                errorList.Add($"{Path.GetFileName(imagePath)} | Không tìm thấy JSON");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorList.Add($"{Path.GetFileName(imagePath)} | {ex.Message}");
                        }

                        Interlocked.Increment(ref processedCount);
                        this.Invoke((MethodInvoker)delegate
                        {
                            progressBar1.Value = (int)processedCount;
                            lblStatus.Text = $"Đang xử lý {processedCount}/{totalCount}...";
                        });
                    });
            });

            // Kết quả
            progressBar1.Visible = false;
            lblStatus.Text = $"HOÀN TẤT! Thành công: {successList.Count} | Lỗi: {errorList.Count}";
            lblStatus.ForeColor = successList.Count > errorList.Count ? System.Drawing.Color.Green : System.Drawing.Color.Red;
            btnProcess.Enabled = true;
            btnSelectFolder.Enabled = true;

            SaveLog();
        }

        private string FindJsonForImage(string imagePath)
        {
            string dir = Path.GetDirectoryName(imagePath);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);

            // Tìm JSON: [tên gốc].json HOẶC [tên gốc]*.json
            string[] patterns = {
        nameWithoutExt + ".json",
        nameWithoutExt + "*.json"
    };

            foreach (string pattern in patterns)
            {
                try
                {
                    var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        return files.OrderBy(f => f.Length).First(); // Ưu tiên file ngắn nhất
                    }
                }
                catch { }
            }

            return null;
        }

        public static string DetectRealFormat(string path)
        {
            try
            {
                byte[] header = new byte[12];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    fs.Read(header, 0, header.Length);

                // JPG
                if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return "jpg";

                // PNG
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return "png";

                // WEBP
                if (header.Length >= 12 &&
                    Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                    Encoding.ASCII.GetString(header, 8, 4) == "WEBP")
                    return "webp";

                // HEIC/HEIF
                if (header.Length >= 12 &&
                    Encoding.ASCII.GetString(header, 4, 4) == "ftyp")
                {
                    string brand = Encoding.ASCII.GetString(header, 8, 4);
                    if (brand == "heic" || brand == "heix" || brand == "hevc") return "heic";
                    if (brand == "mif1" || brand == "avif") return "heif";
                }

                // MP4/MOV
                if (header.Length >= 8 &&
                    Encoding.ASCII.GetString(header, 4, 4) == "ftyp")
                {
                    string brand = Encoding.ASCII.GetString(header, 8, 4);
                    if (brand.StartsWith("qt  ") || brand == "mp41" || brand == "mp42") return Path.GetExtension(path).ToLower().TrimStart('.');
                }

                return Path.GetExtension(path).ToLower().TrimStart('.');
            }
            catch
            {
                return Path.GetExtension(path).ToLower().TrimStart('.');
            }
        }

        private bool ProcessJsonToImage(string jsonPath, ref string imagePath)
        {
            try
            {
                // BƯỚC 1: Đọc JSON trước
                string jsonContent = File.ReadAllText(jsonPath, Encoding.UTF8);
                JObject json = JObject.Parse(jsonContent);

                // BƯỚC 2: Phát hiện định dạng thật
                string realExt = DetectRealFormat(imagePath);
                string currentExt = Path.GetExtension(imagePath).TrimStart('.').ToLower();
                string originalImagePath = imagePath;

                // BƯỚC 3: Đổi tên file nếu cần
                if (!realExt.Equals(currentExt, StringComparison.OrdinalIgnoreCase))
                {
                    string newPath = Path.ChangeExtension(imagePath, realExt);
                    if (!File.Exists(newPath))
                    {
                        File.Move(imagePath, newPath);
                        imagePath = newPath; // CẬP NHẬT imagePath
                    }
                }
                // BƯỚC 4: TÌM LẠI JSON DỰA TRÊN TÊN MỚI (nếu file đổi tên)
                string finalJsonPath = jsonPath;
                if (imagePath != originalImagePath)
                {
                    // Tìm lại JSON theo tên mới
                    string newJsonPath = FindJsonForImage(imagePath);
                    if (newJsonPath != null && File.Exists(newJsonPath))
                    {
                        finalJsonPath = newJsonPath;
                        jsonContent = File.ReadAllText(finalJsonPath, Encoding.UTF8);
                        json = JObject.Parse(jsonContent);
                    }
                    else
                    {
                        // Nếu không tìm thấy → vẫn dùng JSON cũ (có thể không chính xác, nhưng ít lỗi hơn)
                    }
                }
                bool isImage = imagePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".heif", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
                bool isPng = imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
                bool isVideo = imagePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                               imagePath.EndsWith(".mov", StringComparison.OrdinalIgnoreCase);

                var args = new StringBuilder();
                args.AppendLine("-overwrite_original");
                args.AppendLine("-m");

                // DateTime
                var photoTaken = json["photoTakenTime"]?["timestamp"]?.ToString();
                if (long.TryParse(photoTaken, out long timestamp))
                {
                    DateTime dt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                    string exifDate = dt.ToString("yyyy:MM:dd HH:mm:ss");
                    string pngDate = dt.ToString("yyyy-MM-ddTHH:mm:ss");           // PNG (ISO8601)
                    if (isPng)
                    {
                        // PNG DOES NOT SUPPORT EXIF → use tEXt or XMP
                        args.AppendLine($"-PNG:CreationTime={pngDate}");
                        args.AppendLine($"-XMP:CreateDate={exifDate}");
                        args.AppendLine($"-XMP:ModifyDate={exifDate}");
                        args.AppendLine($"-XMP:DateTimeOriginal={exifDate}");
                    }
                    if (isImage)
                    {
                        args.AppendLine($"-DateTimeOriginal={exifDate}");
                        args.AppendLine($"-CreateDate={exifDate}");
                        args.AppendLine($"-ModifyDate={exifDate}");
                    }
                    if (isVideo)
                    {
                        args.AppendLine($"-MediaCreateDate={exifDate}");
                        args.AppendLine($"-MediaModifyDate={exifDate}");
                        args.AppendLine($"-CreateDate={exifDate}");
                        args.AppendLine($"-ModifyDate={exifDate}");
                    }
                }

                // GPS
                var lat = json["geoData"]?["latitude"]?.ToString();
                var lon = json["geoData"]?["longitude"]?.ToString();
                var alt = json["geoData"]?["altitude"]?.ToString();

                if (double.TryParse(lat, out double latitude) && latitude != 0 &&
                    double.TryParse(lon, out double longitude) && longitude != 0)
                {
                    string latRef = latitude >= 0 ? "N" : "S";
                    string lonRef = longitude >= 0 ? "E" : "W";
                    args.AppendLine($"-GPSLatitude={Math.Abs(latitude)}");
                    args.AppendLine($"-GPSLatitudeRef={latRef}");
                    args.AppendLine($"-GPSLongitude={Math.Abs(longitude)}");
                    args.AppendLine($"-GPSLongitudeRef={lonRef}");

                    if (double.TryParse(alt, out double altitude))
                    {
                        string altRef = altitude >= 0 ? "0" : "1";
                        args.AppendLine($"-GPSAltitude={Math.Abs(altitude)}");
                        args.AppendLine($"-GPSAltitudeRef={altRef}");
                    }
                }

                // Description / Title
                string desc = json["description"]?.ToString() ?? json["title"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    desc = desc.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\\\"").Trim();
                    if (desc.Length > 0)
                        args.AppendLine($"-ImageDescription={desc}");
                }
                args.AppendLine(imagePath); // DÙNG imagePath MỚI

                // Chạy exiftool
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "exiftool.exe",
                        Arguments = "-@ -",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();

                // ghi UTF-8 vào stdin
                using (var sw = new StreamWriter(process.StandardInput.BaseStream, Encoding.UTF8)) 
                {
                    
                    sw.Write(args.ToString());
                    sw.Flush(); // Đảm bảo dữ liệu được đẩy ngay

                }


           
                process.WaitForExit(30000);
                if (process.ExitCode != 0)
                {
                    string err = process.StandardError.ReadToEnd();
                    errorList.Add($"{Path.GetFileName(imagePath)} | ExifTool: {err.Trim()}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorList.Add($"{Path.GetFileName(imagePath)} | {ex.Message}");
                return false;
            }
        }

        private void TryDeleteJson(string jsonPath)
        {
            try
            {
                System.Threading.Thread.Sleep(100); // Đảm bảo exiftool đã đọc xong
                if (File.Exists(jsonPath))
                    File.Delete(jsonPath);
            }
            catch { }
        }

        private void SaveLog()
        {
            string logPath = Path.Combine(selectedFolder, "GooglePhotosMetadata_Log.txt");
            using (var sw = new StreamWriter(logPath, false, Encoding.UTF8))
            {
                sw.WriteLine($"=== XỬ LÝ LÚC: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                sw.WriteLine($"Thư mục gốc: {selectedFolder}");
                sw.WriteLine($"Quét đệ quy: {(recursiveScan ? "CÓ" : "KHÔNG")}");
                sw.WriteLine($"Tổng file xử lý: {totalCount}");
                sw.WriteLine();

                if (successList.Any())
                {
                    sw.WriteLine("THÀNH CÔNG:");
                    foreach (var f in successList.OrderBy(x => x))
                        sw.WriteLine(" Done " + f);
                }

                if (errorList.Any())
                {
                    sw.WriteLine("\nLỖI:");
                    foreach (var f in errorList.OrderBy(x => x))
                        sw.WriteLine(" Failed " + f);
                }

                sw.WriteLine($"\nTỔNG: {successList.Count} thành công, {errorList.Count} lỗi.");
            }
            lblStatus.Text += " | Log: GooglePhotosMetadata_Log.txt";
        }
    }
}