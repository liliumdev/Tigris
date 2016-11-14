using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Drawing;
using System.Web;
using SeasideResearch.LibCurlNet;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System.Collections.Specialized;

namespace Tigris
{
    /// <summary>
    /// A simple basic class for HTTP Requests.
    /// </summary>
    public class CURLBrowser
    {
        public string UserAgent = "";
        public string page;
        public HtmlDocument doc;
        public string currentURL;
        public string Proxy = "";
        public string csrfToken = "";

        // The curl thingie
        public Easy easy;

        public CURLBrowser(string proxy)
        {
            easy = new Easy();
            Easy.WriteFunction wf = new Easy.WriteFunction(OnWriteData);

            Random rnd = new Random();
            UserAgent = Common.useragents[rnd.Next(0, Common.useragents.Count)];

            easy.SetOpt(CURLoption.CURLOPT_USERAGENT, UserAgent);
            easy.SetOpt(CURLoption.CURLOPT_WRITEFUNCTION, wf);
            easy.SetOpt(CURLoption.CURLOPT_FOLLOWLOCATION, true);
            easy.SetOpt(CURLoption.CURLOPT_COOKIEFILE, "");
            if (proxy != "")
            {
                string[] proxyDetails = proxy.Split(',');
                easy.SetOpt(CURLoption.CURLOPT_PROXY, proxyDetails[0]);
                easy.SetOpt(CURLoption.CURLOPT_PROXYUSERPWD, proxyDetails[1] + ":" + proxyDetails[2]);
            }

            doc = new HtmlAgilityPack.HtmlDocument();
            Proxy = proxy;
        }

        ~CURLBrowser()
        {
            easy.Cleanup();
        }

        public void Get(string url, string parameters = "", bool xml = false)
        {
            bool success = false;
            int retries = 0;

            while (!success && retries < 3)
            {
                try
                {
                    HttpGet(url + "?" + parameters, xml);
                    CheckCSRFToken();
                    if (!page.Contains("Oops, something went wrong.") && !page.Contains("400 Bad Request"))
                        success = true;
                }
                catch (Exception)
                {
                    retries++;
                }
            }
        }

        public void Post(string url, bool xml, params string[] parameters)
        {
            bool success = false;
            int retries = 0;

            while (!success && retries < 3)
            {
                try
                {
                    HttpPost(url, PostDataString(parameters), xml);
                    CheckCSRFToken();
                    if (!page.Contains("Oops, something went wrong.") && !page.Contains("400 Bad Request"))
                        success = true;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (ex.Message == "Too many automatic redirections were attempted.") throw;
                }
            }
        }

        public void Post(string url, bool xml, string postdata)
        {
            bool success = false;
            int retries = 0;

            while (!success && retries < 3)
            {
                try
                {
                    HttpPost(url, postdata, xml);
                    CheckCSRFToken();
                    if (!page.Contains("Oops, something went wrong.") && !page.Contains("400 Bad Request"))
                        success = true;
                }
                catch (Exception ex)
                {
                    retries++;
                    if (ex.Message == "Too many automatic redirections were attempted.") throw;
                }
            }
        }

        public void Post(string url, string referer, params string[] parameters)
        {
            bool success = false;
            int retries = 0;

            while (!success && retries < 3)
            {
                try
                {
                    HttpPost(url, PostDataString(parameters), false, referer);
                    doc.LoadHtml(page);
                    CheckCSRFToken();
                    if (!page.Contains("Oops, something went wrong.") && !page.Contains("400 Bad Request"))
                        success = true;
                }
                catch (Exception)
                {
                    retries++;
                }
            }
        }

        // eRepublik specific; shouldn't do this =(
        public void CheckCSRFToken()
        {
            if (page.Contains("var csrfToken = '"))
            {
                string page2 = page.Substring(page.IndexOf("var csrfToken = '"));
                csrfToken = page2.Substring(17, page2.IndexOf("';") - 17);
            }
        }

        public string HttpGet(string url, bool xml = false)
        {
            url = url.Replace(" ", "%20");
          //  url = SeasideResearch.LibCurlNet.Curl.Escape(url, url.Length);
            page = "";
            easy.SetOpt(CURLoption.CURLOPT_URL, url);
            easy.SetOpt(CURLoption.CURLOPT_HTTPHEADER, null);

            if (xml)
            {
                Slist headers = new Slist();
                headers.Append("Accept: application/json, text/javascript, */*");
                headers.Append("X_REQUESTED_WITH: XMLHttpRequest");
                easy.SetOpt(CURLoption.CURLOPT_HTTPHEADER, headers);
            }

            easy.SetOpt(CURLoption.CURLOPT_HTTPGET, true);
            //easy.SetOpt(CURLoption.CURLOPT_REFERER, CurrentURL);
            easy.SetOpt(CURLoption.CURLOPT_FOLLOWLOCATION, true);

            easy.Perform();

            easy.GetInfo(CURLINFO.CURLINFO_EFFECTIVE_URL, ref currentURL);

            if (this.page.Contains("<!DOCTYPE"))
                doc.LoadHtml(this.page);

            return this.page;
        }        

        public string HttpPost(string url, string post, bool xml = false, string refer = "")
        {
            post = Encode(post);
            page = "";
            easy.SetOpt(CURLoption.CURLOPT_URL, url);

            easy.SetOpt(CURLoption.CURLOPT_HTTPHEADER, null);
            if (xml)
            {
                Slist headers = new Slist();
                headers.Append("Accept: application/json, text/javascript, */*");
                headers.Append("X_REQUESTED_WITH: XMLHttpRequest");
                easy.SetOpt(CURLoption.CURLOPT_HTTPHEADER, headers);
            }

            easy.SetOpt(CURLoption.CURLOPT_POST, true);
            easy.SetOpt(CURLoption.CURLOPT_POSTFIELDS, post);
            //easy.SetOpt(CURLoption.CURLOPT_REFERER, CurrentURL);
            easy.SetOpt(CURLoption.CURLOPT_FOLLOWLOCATION, true);

            easy.Perform();

            easy.GetInfo(CURLINFO.CURLINFO_EFFECTIVE_URL, ref currentURL);

            if (this.page.Contains("<!DOCTYPE"))
                doc.LoadHtml(this.page);

            return this.page;
        }

        public string RawHttpGet(string url)
        {
            WebRequest req = HttpWebRequest.Create(url);
            req.Method = "GET";
            req.Proxy = null;

            string source;
            using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
            {
                source = reader.ReadToEnd();
            }

            return source;
        }

        // Returns the value of a node specified by the CSS selector
        public string GetValue(string selector)
        {
            HtmlNode node = Select(selector);
            return node != null ? node.Attributes["value"].Value.ToString() : "none";
        }

        // Returns the inner html of a node specified by the CSS selector
        public string GetHtml(string selector)
        {
            HtmlNode node = Select(selector);
            return node != null ? node.InnerHtml : "none";
        }

        public string GetAttribute(string selector, string attribute)
        {
            HtmlNode node = Select(selector);
            return node != null ? node.Attributes[attribute].Value.ToString() : "none";
        }

        // Returns a single node specified by the CSS selector
        public HtmlNode Select(string selector)
        {
            return doc.DocumentNode.QuerySelector(selector);
        }

        // Returns all nodes specified by the CSS selector
        public IEnumerable<HtmlNode> SelectAll(string selector)
        {
            return doc.DocumentNode.QuerySelectorAll(selector);
        }

        // The parameter data should be formatted like the GET URL parameters, i.e.
        // key1=value1&key2=value2&key3=value3
        public NameValueCollection StringToNVC(string data)
        {
            try
            {
                if (data == "" || data.IndexOf('=') == -1)
                    throw new System.Exception("StringToNVC: Invalid POST data [ " + data + " ]");

                // Remove any excess &'s
                data = data.Replace("&&", "&");

                // IndexOf('&') will return -1 if there's only one key/value pair 
                string[] pairs = data.IndexOf('&') == -1 ? new string[] { data } : data.Split('&');

                NameValueCollection prepared_data = new NameValueCollection();

                foreach (string pair in pairs)
                {
                    string[] pairData = pair.Split('=');
                    prepared_data.Add(pairData[0], pairData[1]); // pairData[0] is the key, pairData[1] is the value
                }

                return prepared_data;
            }
            catch (System.Exception)
            {
                throw new System.Exception("StringToNVC: Unknown error [ " + data + " ]");
            }
        }

        // Constructs form data from a list of parameters
        public NameValueCollection PostData(params string[] parameters)
        {
            try
            {
                if (parameters.Length % 2 != 0) throw new System.Exception("PostData: Invalid number of parameters given");

                string postData = "";
                int i = 1;
                foreach (string parameter in parameters)
                {
                    if (i % 2 != 0)
                        postData += parameter + "=";
                    else
                        postData += parameter + "&";
                    i++;
                }
                postData = postData.Substring(0, postData.Length - 1); // Remove the last '&' character
                return StringToNVC(postData);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine(ex.Message);
                throw;
            }
        }

        // Constructs form data from a list of parameters
        public string PostDataString(params string[] parameters)
        {
            try
            {
                if (parameters.Length % 2 != 0) throw new System.Exception("PostData: Invalid number of parameters given");

                string postData = "";
                int i = 1;
                foreach (string parameter in parameters)
                {
                    if (i % 2 != 0)
                        postData += parameter + "=";
                    else
                        postData += parameter + "&";
                    i++;
                }
                postData = postData.Substring(0, postData.Length - 1); // Remove the last '&' character
                return postData;
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine(ex.Message);
                throw;
            }
        }

        public string SolveCaptcha(ref string captchaChallenge, bool auto = false)
        {
            string captchaKey = "6LeWK7wSAAAAAA_QFoHVnY5HwVCb_CETsvrayFhu"; // eRepublik captcha key
            string captchapage = RawHttpGet("http://www.google.com/recaptcha/api/challenge?ajax=1&k=" + captchaKey);

            int g = captchapage.IndexOf("challenge : '");
            captchapage = captchapage.Substring(g + 13);
            g = captchapage.IndexOf("',");
            captchaChallenge = captchapage.Substring(0, g);

            // Download web image
            byte[] data = DownloadImage("http://www.google.com/recaptcha/api/image?c=" + captchaChallenge);


            // Manual captcha prompt
            if (!auto) return Common.CaptchaDialog(data);

            // Check for valid image and solve the captcha using De-Captcher
            // TODO (MEDP): Implement the possibility of custom De-captcher accounts
            //               (user-input ones)
            // TODO (LOWP): Implement some other decaptcher web services, this one seems to
            //               be always overloaded
            if (data != null)
            {
                NameValueCollection nvc = new NameValueCollection();
                nvc.Add("function", "picture2");
                nvc.Add("username", "mobster1930");
                nvc.Add("password", "123654789");
                nvc.Add("pict_type", "0");
                nvc.Add("pict_to", "0");
                string result = HttpUploadFile("http://poster.de-captcher.com", data, "pict", "image/jpeg", nvc);

                string[] responseParams = result.Split('|');

                if (responseParams[0] == "0")
                {
                    if (responseParams.Count() > 5)
                    {
                        return responseParams[5];
                    }
                }
                return "error";
            }

            return "error";
        }

        public void UploadFile(string url, MultiPartForm mf)
        {
            easy.SetOpt(CURLoption.CURLOPT_URL, url);
            easy.SetOpt(CURLoption.CURLOPT_HTTPPOST, mf);
            easy.Perform();

            easy.GetInfo(CURLINFO.CURLINFO_EFFECTIVE_URL, ref currentURL);

            if (this.page.Contains("<!DOCTYPE"))
                doc.LoadHtml(this.page);
        }

        public string HttpUploadFile(string url, byte[] file, string paramName, string contentType, NameValueCollection nvc)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;

            Stream rs = wr.GetRequestStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, file, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            // Now write the file
            rs.Write(file, 0, file.Count());

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp = null;
            try
            {
                wresp = wr.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                return reader2.ReadToEnd();
            }
            catch (Exception)
            {
                if (wresp != null)
                {
                    wresp.Close();
                    wresp = null;
                }
            }
            finally
            {
                wr = null;
            }

            return "";
        }

        public string HttpUploadFile(string url, string file, string paramName, string contentType, NameValueCollection nvc)
        {
            return HttpUploadFile(url, System.IO.File.ReadAllBytes(file), paramName, contentType, nvc);
        }

        /// <summary>
        /// Function to download Image from website
        /// </summary>
        /// <param name="_URL">URL address to download image</param>
        /// <returns>Image</returns>
        public byte[] DownloadImage(string _URL)
        {
            try
            {
                HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)HttpWebRequest.Create(_URL);
                _HttpWebRequest.AllowWriteStreamBuffering = true;
                _HttpWebRequest.UserAgent = this.UserAgent;
                _HttpWebRequest.Referer = "http://www.erepublik.com/";
                _HttpWebRequest.Timeout = 20000;
                WebResponse _WebResponse = _HttpWebRequest.GetResponse();
                byte[] b = null;
                using (Stream stream = _WebResponse.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        byte[] buf = new byte[1024];
                        count = stream.Read(buf, 0, 1024);
                        ms.Write(buf, 0, count);
                    } while (stream.CanRead && count > 0);
                    b = ms.ToArray();
                }

                _WebResponse.Close();
                _WebResponse.Close();

                return b;
            }
            catch (Exception _Exception)
            {
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
                return null;
            }
        }

        /// <summary>
        /// Copies the contents of input to output. Doesn't close either stream.
        /// </summary>
        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }            
               
        public Int32 OnWriteData(Byte[] buf, Int32 size, Int32 nmemb,
        Object extraData)
        {
            page += System.Text.Encoding.UTF8.GetString(buf);
            return size * nmemb;
        }

        public string Encode(string data)
        {
            string encoded = "";
            string[] fields = data.Split('&');
            foreach (string field in fields)
            {
                string[] fieldPair = field.Split('=');
                encoded += HttpUtility.UrlEncode(fieldPair[0]) + "=" + HttpUtility.UrlEncode(fieldPair[1]) + "&";
            }

            return encoded.Substring(0, encoded.Length - 1);
        }

        public void DebugHtml(string html)
        {
            StreamWriter sw = new StreamWriter("debug.html");
            sw.Write(html);
            sw.Close();
        }
    }
}
