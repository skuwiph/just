namespace just
{
    using AngleSharp.Parser.Html;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    throw new ApplicationException("ERROR: No arguments specified!");
                }

                switch (args[0].ToLower())
                {
                    case "init":
                        Initialise(args);
                        break;
                    case "setuser":
                        SetUser(args);
                        break;
                    case "new":
                        NewEntry(args);
                        break;
                    default:
                        Console.WriteLine("ERROR: command not recognised");
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private static JustSettings ReadSettings()
        {
            JustSettings js = null;

            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Formatting = Formatting.Indented;

            using (StreamReader sr = new StreamReader($"just-settings.json"))
            {
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    js = serializer.Deserialize<JustSettings>(reader);
                }
            }

            if (js == null)
            {
                throw new ApplicationException("ERROR: no just-settings.json found in current directory.\r\n\r\nDid you run\r\n\r\n\tjust init beforehand?");
            }

            // NOTE(ian): File.Exists doesn't work on network share or UNC paths
            FileInfo fi = new FileInfo(js.Template);
            bool exists = fi.Exists;

            if (!String.IsNullOrEmpty(js.Template) && !exists)
            {
                throw new ApplicationException($"ERROR: the template '{js.Template}' specified does not exist!");
            }

            if (!Directory.Exists(js.Path))
            {
                Directory.CreateDirectory(js.Path);
            }

            return js;
        }

        private static void Initialise(string[] args)
        {
            JustSettings js = new JustSettings();
            string templateDir = string.Empty;
            string pathToFiles = "doc/user-story/";
            string jiraHostUri = string.Empty;

            for(int i=1; i<args.Length; i++)
            {
                if( args[i].ToLower() == "-t")
                {
                    templateDir = args[i + 1];
                }
                else if(args[i].ToLower() == "-p")
                {
                    pathToFiles = args[i + 1];
                }
                else if (args[i].ToLower() == "-j")
                {
                    jiraHostUri = args[i + 1];
                }
            }

            if (String.IsNullOrEmpty(jiraHostUri))
                throw new ApplicationException("ERROR: You must specify the Jira anchor point with the '-j' option");

            if (!String.IsNullOrEmpty(templateDir))
            {
                js.Template = templateDir;

                FileInfo fi = new FileInfo(js.Template);
                if (!fi.Exists)
                {
                    Console.WriteLine($"WARNING: the template '{js.Template}' does not exist at present. This will become a fatal error if you try to create a new entry without it!");
                }
            }

            js.Path = pathToFiles;
            js.JiraAnchor = jiraHostUri;

            JsonSerializer serializer = new JsonSerializer();
            serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;
            serializer.Formatting = Formatting.Indented;

            using (StreamWriter sw = new StreamWriter($"just-settings.json"))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, js);
                }
            }
        }

        /// <summary>
        /// Create a new entry
        /// 
        /// just new AP-444
        /// 
        /// or
        /// 
        /// just new AP-444 -s
        /// 
        /// to supercede the original
        /// ??
        /// </summary>
        /// <param name="args"></param>
        private static void NewEntry(string[] args)
        {
            if( args.Length < 2)
            {
                Console.WriteLine("ERROR: syntax is:\r\n\r\n\tjust new <Jira Task Code>\r\n\r\n\te.g.\r\n\\tjust new CA-484");
                return;
            }

            _settings = ReadSettings();

            string url = $"https://{_settings.JiraAnchor}/browse/{args[1]}";
            string restUrl = $"https://{_settings.JiraAnchor}/rest/api/2/issue/{args[1]}";
            string responseData = String.Empty;

            string userPasswordB64 = String.Empty;

            if( args.Length == 4)
            {
                if(args[2] == "-u")
                {
                    userPasswordB64 = Base64Encode(args[3]);
                }
            } else
            {
                userPasswordB64 = GetUser();
            }
            string authHeader = $"Authorization: Basic {userPasswordB64}";

            // TODO(ian): add a parameter to do this from behind a proxy

            Console.WriteLine($"Reading {restUrl}");

            // NOTE(ian): this is the vanilla approach
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(restUrl);

            // Add basic auth
            request.Headers.Add(authHeader);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Stream receiveStream = response.GetResponseStream();
                StreamReader readStream = null;

                if (response.CharacterSet == null)
                {
                    readStream = new StreamReader(receiveStream);
                }
                else
                {
                    readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                }

                responseData = readStream.ReadToEnd();

                response.Close();
                readStream.Close();
            }
            else if(response.StatusCode == HttpStatusCode.NotFound)
            {
                // Empty response code just sets up a blank entry
            }
            else
            {
                Console.WriteLine($"ERROR: Got HTTP Status Code {response.StatusCode}: {response.StatusDescription}");
                return;
            }

            CreateDocumentAndWait(args[1], url, responseData, true);

        }
        
        private static void SetUser(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ApplicationException("ERROR: setuser requires the parameter 'username:password'");
            }

            if(!Directory.Exists(GetLocalSettingsPath(false)))
            {
                Directory.CreateDirectory(GetLocalSettingsPath(false));
            }

            using (StreamWriter sr = new StreamWriter(GetLocalSettingsPath(), false, Encoding.UTF8))
            {
                sr.WriteLine(Base64Encode(args[1]));
                sr.Close();
            }
        }

        private static string GetUser()
        {
            string user = String.Empty;

            using (StreamReader sr = new StreamReader(GetLocalSettingsPath()))
            {
                user = sr.ReadToEnd();
            }

            return user;
        }

        private static string GetLocalSettingsPath(bool includeFile = true)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "aifs", "just", includeFile ? "su.data" : String.Empty);
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static void CreateDocumentAndWait(string jiraCode, string jiraUrl, string responseData, bool isJson = false)
        {
            JustDocument jd;

            if (!String.IsNullOrEmpty(responseData)) {
                if (isJson)
                    jd = ParseJson(jiraUrl, responseData);
                else
                    jd = ParsePage(jiraUrl, responseData);
            }
            else
            {
                jd = CreateDocument(jiraUrl, jiraCode, "Description ...");
            }

            // do we have a template?
            

            if (!jd.Edit())
            {
                Console.WriteLine("User aborted edit.");
            }
        }

        private static JustDocument ParseJson(string jiraUrl, string responseData)
        {
            string touchedTitle = "";
            string userContent = "";
            string createdBy = "";

            dynamic json = JsonConvert.DeserializeObject(responseData);

            touchedTitle = $"[{json.key}] - {json.fields.summary.Value}";

            if (json.fields.description != null )
                userContent = json.fields.description.Value.Replace("\r\n", "\r\n  ");

            createdBy = json.fields.creator.displayName.Value;

            DateTime createdDate = json.fields.created.Value;

            JustDocument jd = CreateDocument(jiraUrl, touchedTitle, userContent, createdBy);
            jd.CreatedDate = createdDate;

            return jd;
        }

        private static JustDocument ParsePage(string jiraUrl, string responseData)
        {
            var parser = new HtmlParser();
            var document = parser.Parse(responseData);

            var login = document.All.Where(m => m.LocalName == "div" && m.Id == "login-panel").FirstOrDefault();

            if (login != null)
                throw new ApplicationException("Jira is requesting authentication");

            var JiraTitle = document.QuerySelector("title");

            string touchedTitle = JiraTitle.TextContent.Replace(" - JIRA", "");

            var userContent = document.All.Where(m => m.LocalName == "div" && m.ClassList.Contains("user-content-block")).FirstOrDefault();

            return CreateDocument(jiraUrl, touchedTitle, userContent.TextContent.Trim());
        }

        private static JustDocument CreateDocument(string jiraUrl, string title, string description, string createdBy = "")
        {
            return new JustDocument(_settings)
            {
                Title = title.Trim(),
                Description = description.Trim(),
                JiraUrl = jiraUrl,
                CreatedBy = createdBy
            };
        }

        private static JustSettings _settings;
    }

    class JustDocument
    {
        public JustDocument(JustSettings settings)
        {
            _settings = settings;
            CreatedBy = "Unknown";
            CreatedDate = DateTime.Now;

            if (String.IsNullOrEmpty(_settings.Template))
            {
                _template = "# _TITLE\r\n\r\n## _CREATEDBY on _DATE\r\n\r\n## Reference\r\n\r\n_URL\r\n## Description\r\n\r\n_DESCRIPTION\r\n\r\n";
            }
            else
            {
                ReadTemplate(_settings.Template);
            }
        }

        public string FileName => $"{Title.Trim().Replace(" ", "-").Replace("\"","").Replace("---", "-").ToLower()}.md";
        //public string SafeFileName
        //{
        //    get
        //    {
        //        var filename = FileName;
        //        Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c, '-'));
        //        return filename;
        //    }
        //}

        public string Title { get; set; }    
        public string Description { get; set; }
        public string JiraUrl { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedDataDisplay => $"{CreatedDate.ToShortDateString()}";

        // Remainder goes here

        public bool Edit()
        {
            string temp = Path.Combine(Path.GetTempPath(), FileName);

            // Must write this first to the temp directory
            Write(Path.GetTempPath());

            DateTime created = File.GetLastWriteTimeUtc(temp);

            ProcessStartInfo pInfo = new ProcessStartInfo();
            pInfo.FileName = temp;
            Process p = Process.Start(pInfo);

            p.WaitForInputIdle();                               // Wait for the window to finish loading.
            p.WaitForExit();                                    // Wait for the process to end.

            DateTime write = File.GetLastWriteTimeUtc(temp);

            if (write > created)
            {
                // Copy temp file to settings.Path location
                File.Move(temp, Path.Combine(_settings.Path, FileName));
            }
            else
            {
                File.Delete(temp);

            }

            return write > created;
        }

        public void Write(string path)
        {
            StringBuilder s = new StringBuilder(_template);
            s.Replace("_TITLE", Title);
            s.Replace("_CREATEDBY", CreatedBy);
            s.Replace("_DATE", CreatedDataDisplay);
            s.Replace("_DESCRIPTION", Description);
            s.Replace("_URL", JiraUrl);
            using (StreamWriter sr = new StreamWriter(Path.Combine(path, FileName.ToLower()), false, Encoding.UTF8))
            {
                sr.WriteLine(s.ToString());
                sr.Close();
            }

        }

        public void ReadTemplate(string pathToTemplate)
        {

            using (StreamReader sr = new StreamReader(pathToTemplate))
            {
                _template = sr.ReadToEnd();
            }

        }

        private JustSettings _settings;
        private string _template;
    }

    class JustSettings
    {
        public string Path { get; set; }
        public string Template { get; set; }
        public string JiraAnchor { get; set; }
    }
    
}
