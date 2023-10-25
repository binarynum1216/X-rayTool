using System.IO;
using System.Net.Http;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace Download_Upload
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly Dictionary<string, string> config = new Dictionary<string, string>();

        private int downloadInterval;                                                   // Interval in seconds at which the REST API should be queried for available jobs
        private string? downloadApiUrl;                                                 // Path to Rest API to download new jobs
        private string? downloadFolder;                                                 // Local path in which the downloaded files are stored(might be an UNC path)
        private string? downloadCall;                                                   // Optionally specifyable local system call to notify a program of new downloaded files
        private string? downloadEncoding;                                               // Encoding format for downloaded files

        private int downloadInterval2;                                                  // Interval in seconds at which the REST API should be queried for available jobs
        private string? downloadApiUrl2;                                                // Path to Rest API to download new jobs
        private string? downloadFolder2;                                                // Local path in which the downloaded files are stored(might be an UNC path)
        private string? downloadCall2;                                                  // Optionally specifyable local system call to notify a program of new downloaded files
        private string? downloadEncoding2;                                              // Encoding format for downloaded files

        private int uploadInterval;                                                     // Interval in seconds at which a local folder should be checked for new files to be uploaded
        private string? uploadApiUrl;                                                   // Path to REST API to upload new results
        private string? uploadFolder;                                                   // Local path in which the result files are stored (might be an UNC path)
        private string? uploadFile;                                                     // Only files that match this naming scheme should be uploaded

        private int viewerInterval;                                                     // Interval in seconds at which the REST API should be queried for viewer requests
        private string? viewerApiUrl;                                                   // path to REST API to retrieve requests to open an image in a local viewer
        private string? viewerCall;                                                     // local system call to open X-ray images in a local viewer
        private string? logging;

        private int result_interval;
        private string? result_url;
        private string? result_folder_gdt;
        private string? result_folder_image;
        private string? result_file;
        private string? result_encoding;
        private string? result_field_animal_id;
        private string? result_field_image_file;
        private string? result_field_short_report;
        private string? result_delete_image;

        private string? download_taskkill;
        private string? download_taskkill2;
        private string? viewer_taskkill;
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;                                                           // Logger 
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string currentDirectory = AppContext.BaseDirectory;
            string configFile = Path.Combine(currentDirectory, "config.txt");            // Config File Path         
            string[] lines = System.IO.File.ReadAllLines(configFile);                    // Read config file

            int intervalThreshold = 15000;                                               // The threahold for intervals

            // Substitute all configuration values

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains('='))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    while (i < lines.Length - 1 && !lines[i + 1].Contains('='))
                    {
                        i++;
                        value += " " + lines[i].Trim();
                    }

                    config[key] = value;
                }
            }
            downloadInterval = Convert.ToInt32(config["download_interval"]) * 1000 > intervalThreshold ? Convert.ToInt32(config["download_interval"]) * 1000 : intervalThreshold;
            downloadApiUrl = config["download_url"].Trim();
            downloadFolder = config["download_folder"].Trim();
            downloadCall = config["download_call"];
            downloadEncoding = config["download_encoding"].Trim();
            logging = config["logging"];



            if (config["download_interval2"] != "")
            {
                downloadInterval2 = Convert.ToInt32(config["download_interval2"]) * 1000 > intervalThreshold ? Convert.ToInt32(config["download_interval2"]) * 1000 : intervalThreshold;
            }
            else
            {
                downloadInterval2 = 0;
            }
            downloadApiUrl2 = config["download_url2"].Trim();
            downloadFolder2 = config["download_folder2"].Trim();
            downloadCall2 = config["download_call2"];
            downloadEncoding2 = config["download_encoding2"].Trim();

            if (config["upload_interval"] != "")
            {
                uploadInterval = Convert.ToInt32(config["upload_interval"]) * 1000 > intervalThreshold ? Convert.ToInt32(config["upload_interval"]) * 1000 : intervalThreshold;
            }
            else
            {
                uploadInterval = 0;
            }
            uploadApiUrl = config["upload_url"].Trim();
            uploadFolder = config["upload_folder"].Trim();
            uploadFile = config["upload_file"].Trim();

            if (config["viewer_interval"] != "")
            {
                viewerInterval = Convert.ToInt32(config["viewer_interval"]) * 1000 > intervalThreshold ? Convert.ToInt32(config["viewer_interval"]) * 1000 : intervalThreshold;
            }
            else
            {
                viewerInterval = 0;
            }

            viewerApiUrl = config["viewer_url"].Trim();
            viewerCall = config["viewer_call"];

            if (!isValidUrl(downloadApiUrl))
            {
                if (logging == "true") _logger.LogInformation("Download URL is not valid: {time}\n", DateTimeOffset.Now);
            }

            if (!isValidUrl(downloadApiUrl2))
            {
                if (logging == "true") _logger.LogInformation("Download URL2 is not valid: {time}\n", DateTimeOffset.Now);
            }

            if (!isValidUrl(uploadApiUrl))
            {
                if (logging == "true") _logger.LogInformation("Upload URL is not valid: {time}\n", DateTimeOffset.Now);
            }

            if (!isValidUrl(viewerApiUrl))
            {
                if (logging == "true") _logger.LogInformation("Viewer URL is not valid: {time}\n", DateTimeOffset.Now);
            }

            if (config["result_interval"] != "")
            {
                result_interval = Convert.ToInt32(config["result_interval"]) * 1000 > intervalThreshold ? Convert.ToInt32(config["result_interval"]) * 1000 : intervalThreshold;
            }
            string? tmp = logging.Trim();
            logging = tmp;


            result_url = config["result_url"].Trim();
            result_folder_gdt = config["result_folder_gdt"].Trim();
            result_folder_image = config["result_folder_image"].Trim();
            result_file = config["result_file"].Trim();
            result_encoding = config["result_encoding"].Trim();
            result_field_animal_id = config["result_field_animal_id"].Trim();
            result_field_image_file = config["result_field_image_file"].Trim();
            result_field_short_report = config["result_field_short_report"].Trim();
            result_delete_image = config["result_delete_image"].Trim();

            download_taskkill = config["download_taskkill"].Trim();
            download_taskkill2 = config["download_taskkill2"].Trim();
            viewer_taskkill = config["viewer_taskkill"].Trim();

           /* 
            Console.WriteLine("dowload_taskkill : " + download_taskkill);
            Console.WriteLine("download_taskkill2 : " + download_taskkill2);
            Console.WriteLine("viewer_taskkill : " + viewer_taskkill);
           */

            while (!stoppingToken.IsCancellationRequested)
            {
                // Task for download
                Task taskDownload = Task.Run(async () =>
                {
                    // If download url2 is valid, download folder is not empty, download interval is defined
                    if (isValidUrl(downloadApiUrl) && downloadFolder.Trim() != "" && downloadInterval != 0)
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            if (logging == "true") _logger.LogInformation("Download Task running at: {time}\n", DateTimeOffset.Now);

                            Stopwatch stopwatch = Stopwatch.StartNew();
                            // Creat the http client for http request

                            using (HttpClient httpClient = new HttpClient())
                            {
                                // Get the response from the download url2 
                                HttpResponseMessage response;
                                try
                                {
                                    response = await httpClient.GetAsync(downloadApiUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        string responseBody;
                                        try
                                        {
                                            responseBody = await response.Content.ReadAsStringAsync();
                                            string? fileName = null;
                                            string filePath;
                                            JObject jsonObject = JObject.Parse(responseBody);
                                            int? Status = null;
                                            if (jsonObject.TryGetValue("status", out JToken? statueToken))
                                            {
                                                Status = statueToken.Value<int>();
                                                if (Status == 1)
                                                {
                                                    // Create download folder2, if it doesn't exist,
                                                    if (!Directory.Exists(downloadFolder))
                                                    {
                                                        Directory.CreateDirectory(downloadFolder);
                                                    }

                                                    if (jsonObject.TryGetValue("file", out JToken? fileNameToken))
                                                    {
                                                        fileName = fileNameToken.Value<string>();
                                                    }

                                                    if (!string.IsNullOrEmpty(fileName))
                                                    {
                                                        filePath = Path.Combine(downloadFolder, fileName);
                                                        string decodedString = "";

                                                        if (jsonObject.TryGetValue("content", out JToken? contentToken))
                                                        {
                                                            string? base64EncodedContent = contentToken.Value<string>();

                                                            if (!string.IsNullOrEmpty(base64EncodedContent))
                                                            {
                                                                byte[] data = Convert.FromBase64String(base64EncodedContent);
                                                                decodedString = System.Text.Encoding.UTF8.GetString(data);
                                                            }
                                                        }

                                                        var instance = CodePagesEncodingProvider.Instance;
                                                        Encoding.RegisterProvider(instance);

                                                        if (downloadEncoding == "")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString);
                                                        }
                                                        else if (downloadEncoding == "UTF-8")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, new UTF8Encoding(false));
                                                        }
                                                        else if (downloadEncoding == "ISO-8859-1")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, Encoding.Latin1);
                                                        }
                                                        else if (downloadEncoding == "ISO-8859-15")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, Encoding.GetEncoding("ISO-8859-15"));
                                                        }

                                                        if (download_taskkill != null)
                                                        {
                                                            string result = download_taskkill.Substring(0, download_taskkill.Length - 4);

                                                            Console.WriteLine("This is starting of download_taskkill");
                                                            SendMessageToUserApp("A", result);
                                                            await Task.Delay(TimeSpan.FromSeconds(5));
                                                            Console.WriteLine("This is ending of download_taskkill");
                                                        }

                                                        if (downloadCall.Trim() != "" && System.IO.File.Exists(downloadCall.Split(' ')[0]))
                                                        {
                                                            downloadCall = downloadCall.Replace("#file#", fileName);
                                                            string[] parts = downloadCall.Split(" ");
                                                            string Havetosendvalue = string.Join(" ", parts.Skip(1));
                                                            string Havetorunexe = downloadCall.Split(' ')[0];
                                                            SendMessageToUserApp(Havetorunexe, Havetosendvalue);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                            stopwatch.Stop();
                            int actualDelay = downloadInterval - (int)stopwatch.ElapsedMilliseconds;
                            if (actualDelay <= 0) actualDelay = 0;
                            await Task.Delay(actualDelay, stoppingToken);
                        }
                    }
                });

                // Task for download2
                Task taskDownload2 = Task.Run(async () =>
                {
                    // If download url2 is valid, download folder is not empty, download interval is defined
                    if (isValidUrl(downloadApiUrl2) && downloadFolder2.Trim() != "" && downloadInterval2 != 0)
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            if (logging == "true") _logger.LogInformation("Download2 Task running at: {time}\n", DateTimeOffset.Now);

                            Stopwatch stopwatch = Stopwatch.StartNew();
                            // Creat the http client for http request
                            using (HttpClient httpClient = new HttpClient())
                            {
                                // Get the response from the download url2 
                                HttpResponseMessage response;
                                try
                                {
                                    response = await httpClient.GetAsync(downloadApiUrl2);
                                    // If the response is successful
                                    if (response.IsSuccessStatusCode)
                                    {
                                        string responseBody;
                                        try
                                        {
                                            responseBody= await response.Content.ReadAsStringAsync();
                                            string? fileName = null;
                                            string filePath;
                                            JObject jsonObject = JObject.Parse(responseBody);
                                            int? Status = null;
                                            if (jsonObject.TryGetValue("status", out JToken? statueToken))
                                            {
                                                Status = statueToken.Value<int>();
                                                if (Status == 1)
                                                {
                                                    // Create download folder2, if it doesn't exist,
                                                    if (!Directory.Exists(downloadFolder2))
                                                    {
                                                        Directory.CreateDirectory(downloadFolder2);
                                                    }

                                                    if (jsonObject.TryGetValue("file", out JToken? fileNameToken))
                                                    {
                                                        fileName = fileNameToken.Value<string>();
                                                    }
                                                    if (!string.IsNullOrEmpty(fileName))
                                                    {
                                                        filePath = Path.Combine(downloadFolder2, fileName);
                                                        string decodedString = "";

                                                        if (jsonObject.TryGetValue("content", out JToken? contentToken))
                                                        {
                                                            string? base64EncodedContent = contentToken.Value<string>();

                                                            if (!string.IsNullOrEmpty(base64EncodedContent))
                                                            {
                                                                byte[] data = Convert.FromBase64String(base64EncodedContent);
                                                                decodedString = System.Text.Encoding.UTF8.GetString(data);
                                                            }
                                                        }

                                                        var instance = CodePagesEncodingProvider.Instance;
                                                        Encoding.RegisterProvider(instance);

                                                        if (downloadEncoding2 == "")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString);
                                                        }
                                                        else if (downloadEncoding2 == "UTF-8")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, new UTF8Encoding(false));
                                                        }
                                                        else if (downloadEncoding2 == "ISO-8859-1")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, Encoding.Latin1);
                                                        }
                                                        else if (downloadEncoding2 == "ISO-8859-15")
                                                        {
                                                            System.IO.File.WriteAllText(filePath, decodedString, Encoding.GetEncoding("ISO-8859-15"));
                                                        }

                                                        if (download_taskkill2 != null)
                                                        {
                                                            string result = download_taskkill2.Substring(0, download_taskkill2.Length - 4);

                                                            Console.WriteLine("This is starting of download_taskkill2");
                                                            SendMessageToUserApp("A", result);
                                                            await Task.Delay(TimeSpan.FromSeconds(5));
                                                            Console.WriteLine("This is ending of download_taskkill2");
                                                        }

                                                        if (downloadCall2.Trim() != "" && System.IO.File.Exists(downloadCall2.Split(' ')[0]))
                                                        {
                                                            downloadCall2 = downloadCall2.Replace("#file#", fileName);
                                                            string[] parts = downloadCall2.Split(" ");
                                                            string Havetosendvalue = string.Join(" ", parts.Skip(1));
                                                            string Havetorunexe = downloadCall2.Split(' ')[0];
                                                            SendMessageToUserApp(Havetorunexe, Havetosendvalue);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }

                            stopwatch.Stop();
                            int actualDelay = downloadInterval2 - (int)stopwatch.ElapsedMilliseconds;
                            if (actualDelay <= 0) actualDelay = 0;

                            await Task.Delay(actualDelay, stoppingToken);
                        }
                    }
                });

                // Task for upload
                Task taskUpload = Task.Run(async () =>
                {
                    if (isValidUrl(uploadApiUrl) && uploadInterval != 0 && uploadFolder.Trim() != "" && uploadFile.Trim() != "")
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            if (logging == "true") _logger.LogInformation("Upload Task running at: {time}\n", DateTimeOffset.Now);

                            Stopwatch stopwatch = Stopwatch.StartNew();

                            string fileNameWithoutExtension = uploadFile.Split(".")[0];
                            string fileExtension = uploadFile.Split(".")[1];
                            if (Directory.Exists(uploadFolder))
                            {
                                List<string> matchingFiles = GetMatchingFiles(uploadFolder, uploadFile);
                           
                                foreach (string file in matchingFiles)
                                {
                                    using HttpClient httpClient = new HttpClient();

                                    using var formData = new MultipartFormDataContent();

                                    //byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(Path.Combine(uploadFolder, file));
                                    var fileContent = new ByteArrayContent(await System.IO.File.ReadAllBytesAsync(Path.Combine(uploadFolder, file)));

                                    fileContent.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                                    {
                                        Name = "content",
                                        FileName = file
                                    };

                                    formData.Add(new StringContent(file), "file");
                                    formData.Add(fileContent);
                                    //formData.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");

                                    var response = await httpClient.PostAsync(uploadApiUrl, formData);

                                    if (response.IsSuccessStatusCode)
                                    {
                                        System.IO.File.Delete(Path.Combine(uploadFolder, file));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Failed to upload file. Status Code; {response.StatusCode}. Reason: {response.ReasonPhrase}");
                                    }
                                }

                            }

                            stopwatch.Stop();

                            int actualDelay = uploadInterval - (int)stopwatch.ElapsedMilliseconds;
                            if (actualDelay <= 0) actualDelay = 0;

                            await Task.Delay(actualDelay, stoppingToken);
                        }
                    }
                });

                // Task for viewer
                Task taskViewer = Task.Run(async () =>
                {
                    if (isValidUrl(viewerApiUrl) && viewerInterval != 0 && viewerCall.Trim() != "")
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            if (logging == "true") _logger.LogInformation("Viewer Task running at: {time}\n", DateTimeOffset.Now);

                            Stopwatch stopwatch = Stopwatch.StartNew();

                            using (HttpClient httpClient = new HttpClient())
                            {
                                HttpResponseMessage response;
                                try
                                {
                                     response = await httpClient.GetAsync(viewerApiUrl);
                                    if (response.IsSuccessStatusCode)
                                    {

                                        string responseBody;
                                        try
                                        {
                                            responseBody= await response.Content.ReadAsStringAsync();
                                            JObject jsonObject = JObject.Parse(responseBody);
                                            int? Status = null;
                                            if (jsonObject.TryGetValue("status", out JToken? statueToken))
                                            {
                                                Status = statueToken.Value<int>();
                                                if (Status == 1)
                                                {
                                                    string? fileName = null;
                                                    if (jsonObject.TryGetValue("file", out JToken? filenameToken))
                                                    {
                                                        fileName = filenameToken.Value<string>();
                                                    }
                                                    if (!string.IsNullOrEmpty(fileName))
                                                    {
                                                        if (viewer_taskkill != null)
                                                        {
                                                            string result = viewer_taskkill.Substring(0, viewer_taskkill.Length - 4);
                                                            Console.WriteLine("This is starting of download_taskkill");
                                                            
                                                            SendMessageToUserApp("A", result);
                                                            await Task.Delay(TimeSpan.FromSeconds(5));
                                                            
                                                            Console.WriteLine("This is ending of download_taskkill");
                                                        }

                                                        if (System.IO.File.Exists(viewerCall.Split(' ')[0]))
                                                        {
                                                            viewerCall = viewerCall.Replace("#file#", fileName);
                                                            string[] parts = viewerCall.Split(" ");
                                                            string Havetosendvalue = string.Join(" ", parts.Skip(1));
                                                            SendMessageToUserApp(viewerCall.Split(' ')[0], Havetosendvalue);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (viewer_taskkill != null)
                                                        {
                                                            string result = viewer_taskkill.Substring(0, viewer_taskkill.Length - 4);
                                                            Console.WriteLine("This is starting of viewer_taskkill");

                                                            SendMessageToUserApp("A", result);
                                                            await Task.Delay(TimeSpan.FromSeconds(5));

                                                            Console.WriteLine("This is ending of viewer_taskkill");
                                                        }

                                                        if (System.IO.File.Exists(viewerCall.Split(' ')[0]))
                                                        {
                                                            viewerCall = viewerCall.Replace("#file#", fileName);
                                                            string[] parts = viewerCall.Split(" ");
                                                            string Havetosendvalue = "Nonefilename";
                                                            SendMessageToUserApp(viewerCall.Split(' ')[0], Havetosendvalue);
                                                        }
                                                    }
                                                }
                                            }
                                        }                       
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                            }
                            stopwatch.Stop();

                            int actualDelay = viewerInterval - (int)stopwatch.ElapsedMilliseconds;
                            if (actualDelay <= 0) actualDelay = 0;

                            await Task.Delay(actualDelay, stoppingToken);
                        }
                    }
                });

                // Task for result
                Task taskResult = Task.Run(async () =>
                {
                    if (isValidUrl(result_url))
                    {
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            int success_flg = 0;
                            if (logging == "true") _logger.LogInformation("result Task running at: {time}\n", DateTimeOffset.Now);
                            string? _animal_id = null;
                            string? _short_report = null;
                            string? _image_name = null;

                            Stopwatch stopwatch = Stopwatch.StartNew();
                            if (Directory.Exists(result_folder_gdt))
                            {
                                string[] files = Directory.GetFiles(result_folder_gdt);

                                foreach (string file in files)
                                {
                                    string fileName = Path.GetFileName(file);
                                    if (IsMatchingFile(fileName, result_file))
                                    {
                                        try
                                        {
                                            var instance = CodePagesEncodingProvider.Instance;
                                            Encoding.RegisterProvider(instance);

                                            Encoding encoding;
                                            if (result_encoding == "UTF-8")
                                            {
                                                encoding = Encoding.UTF8;
                                            }
                                            else if (result_encoding == "ISO-8859-1")
                                            {
                                                encoding = Encoding.Latin1;
                                            }
                                            else
                                            {
                                                encoding = Encoding.GetEncoding("ISO-8859-15");
                                            }
                                            StreamReader reader = new StreamReader(file, encoding);

                                            while (true)
                                            {
                                                string? line = reader.ReadLine();
                                                if (line == null)
                                                    break;
                                                if (line.Length >= 3)                                                          // Ensure the line has at least 3 characters
                                                {
                                                    string firstPart = line.Substring(3);                                      // Remove the first 3 characters

                                                    if (firstPart.StartsWith(result_field_animal_id))
                                                    {
                                                        string extraPart = firstPart.Substring(result_field_animal_id.Length); // Extract the extra part
                                                        _animal_id = extraPart;
                                                        //Console.WriteLine($"animal_id '{result_field_animal_id}' matches  " + extraPart);
                                                    }

                                                    if (firstPart.StartsWith(result_field_image_file))
                                                    {
                                                        string extraPart = firstPart.Substring(result_field_image_file.Length); // Extract the extra part
                                                        _image_name = extraPart;
                                                        //Console.WriteLine($"image_file '{result_field_image_file}' matches  " + extraPart);
                                                    }

                                                    if (firstPart.StartsWith(result_field_short_report))
                                                    {
                                                        string extraPart = firstPart.Substring(result_field_short_report.Length); // Extract the extra part
                                                        _short_report = extraPart;
                                                        //Console.WriteLine($"short_report '{result_field_short_report}' matches  " + extraPart);
                                                    }
                                                }
                                                else
                                                {
                                                    // Console.WriteLine($"Line '{line}' is too short.");                            // Handle lines with less than 3 characters (if needed)
                                                }
                                            }
                                            reader.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Failed to read file {file}. Error: {ex.Message}");
                                        }
                                    }
                                }
                                if (_animal_id != null && _image_name != null)
                                {
                                    // Get a list of all files in the directory
                                    string[] allFiles = Directory.GetFiles(result_folder_image);
                                    // Find the first file whose name matches _image_name
                                    string? matchingFile = allFiles.FirstOrDefault(file => Path.GetFileName(file) == _image_name);
                                    Console.WriteLine(_animal_id + " " + _short_report + " " + _image_name + " " + matchingFile);

                                    if (matchingFile != null)
                                    {
                                        using (var httpClient = new HttpClient())
                                        using (MultipartFormDataContent? content = new MultipartFormDataContent())
                                        {
                                            // Set the character encoding to UTF-8

                                            if (content.Headers.ContentType != null)
                                            {
                                                content.Headers.ContentType.CharSet = "UTF-8";
                                            }

                                            // Add the animal_id parameter
                                            content.Add(new StringContent(_animal_id, Encoding.UTF8), "animal_id");

                                            // Add the short_report parameter

                                            if (_short_report != null) content.Add(new StringContent(_short_report, Encoding.UTF8), "short_report");
                                            else content.Add(new StringContent(""), "short_report");

                                            // Create a stream for the file
                                            using (var fileStream = System.IO.File.OpenRead(matchingFile))
                                            {
                                                var fileContent = new StreamContent(fileStream);
                                                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                                                {
                                                    Name = "image",
                                                    FileName = Path.GetFileName(matchingFile)
                                                };
                                                // Add the file as a multipart form data
                                                content.Add(fileContent);

                                                // Send the request to the REST API
                                                var response = await httpClient.PostAsync(result_url, content);

                                                // Check the response
                                                if (response.IsSuccessStatusCode)
                                                {
                                                    success_flg = 1;
                                                    Console.WriteLine("Upload Success");
                                                    // Handle a successful response
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Upload failed");
                                                    // Handle an unsuccessful response
                                                }
                                            }
                                            if (success_flg == 1)
                                            {
                                                if (result_delete_image == "true")
                                                {
                                                    try
                                                    {
                                                        // Check if the file exists before attempting to delete it.
                                                        if (System.IO.File.Exists(matchingFile))
                                                        {
                                                            // Delet image file.
                                                            System.IO.File.Delete(matchingFile);
                                                            Console.WriteLine($"File '{matchingFile}' has been deleted.");
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine($"File '{matchingFile}' does not exist.");
                                                        }

                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine($"An error occurred: {ex.Message}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("There is no certain image file");
                                        // No matching file found
                                        // Handle the case where no matching file is found
                                    }

                                }
                                // Delet gdt file.
                                string[] _files = Directory.GetFiles(result_folder_gdt);

                                foreach (string _file in _files)
                                {
                                    string _fileName = Path.GetFileName(_file);
                                    if (IsMatchingFile(_fileName, result_file))
                                    {
                                        System.IO.File.Delete(_file);
                                        Console.WriteLine($"File '{_file}' has been deleted.");
                                    }
                                }
                            }
                            stopwatch.Stop();
                            int actualDelay = result_interval - (int)stopwatch.ElapsedMilliseconds;
                            if (actualDelay < 0) actualDelay = 0;
                            await Task.Delay(actualDelay, stoppingToken);
                        }
                    }
                });

                // Wait until all the tasks are completed
                await Task.WhenAll(taskDownload, taskDownload2, taskUpload, taskViewer, taskResult);
            }
        }


        static void SendMessageToUserApp(string yExePath, string havetosendvalue)
        {
            Console.WriteLine($"Exepath:{yExePath} Sentdata:{havetosendvalue}");
            string message = $"{yExePath}|{havetosendvalue}";
            using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "test_pipe", PipeDirection.Out))
            {
                pipeClient.Connect();
                using (StreamWriter sw = new StreamWriter(pipeClient))
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(message);
                }
            }
        }


        // Get the files matching with search pattern in the folder
        private static List<string> GetMatchingFiles(string folderPath, string searchPattern)
        {
            List<string> matchingFiles = new List<string>();

            string[] allFiles = Directory.GetFiles(folderPath);

            foreach (string file in allFiles)
            {

                string fileName = Path.GetFileName(file);

                if (IsMatchingFile(fileName, searchPattern))
                {
                    matchingFiles.Add(fileName);
                }
            }

            return matchingFiles;
        }
        // Check if the file name matches the search pattern
        private static bool IsMatchingFile(string fileName, string searchPattern)
        {
            string fileNameWithOutExtension = fileName.Split('.')[0];
            string fileExtension = fileName.Split('.')[1];
            string searchPatternFileName = searchPattern.Split('.')[0];
            string searchPatternFileExtension = searchPattern.Split('.')[1];

            if (searchPatternFileExtension.Trim() == "*")
            {
                searchPatternFileExtension = fileExtension;
            }

            if (searchPatternFileName.Trim() == "*")
            {
                searchPatternFileName = fileNameWithOutExtension;
            }
            else if (searchPatternFileName.Contains('*'))
            {
                searchPatternFileName = searchPatternFileName.Substring(0, searchPatternFileName.Length - 1);
            }

            if (searchPattern.Contains("_."))
            {
                searchPatternFileName = searchPatternFileName.Substring(0, searchPatternFileName.Length - 1);
            }

            if ((fileNameWithOutExtension == searchPatternFileName || fileNameWithOutExtension.StartsWith(searchPatternFileName)) && (fileExtension == searchPatternFileExtension || fileExtension.StartsWith(searchPatternFileExtension)))
            {
                return true;
            }
            return false;
        }

        // Check if a url is valid
        private static bool isValidUrl(string url)
        {
            string[] validUrls = { "https://www.vetpraxis.de", "https://www.vetpraxis.online", "https://www.vetpraxis.info", "https://www.vetprax.is", "https://www.vetpraxis-rest.de" };

            for (int i = 0; i < validUrls.Length; i++)
            {
                if (url.StartsWith(validUrls[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}