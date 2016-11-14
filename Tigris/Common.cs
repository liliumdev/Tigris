using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

namespace Tigris
{
    public static class ExtensionMethods
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    public static class Common
    {
        public static string Base64_Encode(string toEncode)
        {
            byte[] toEncodeAsBytes
                  = System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);
            string returnValue
                  = System.Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }

        public static string Base64_Decode(string encodedData)
        {
            byte[] encodedDataAsBytes
                = System.Convert.FromBase64String(encodedData);
            string returnValue =
               System.Text.ASCIIEncoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }

        public static string CreateExceptionString(Exception e)
        {
            StringBuilder sb = new StringBuilder();
            CreateExceptionString(sb, e, string.Empty);

            return sb.ToString();
        }

        private static void CreateExceptionString(StringBuilder sb, Exception e, string indent)
        {
            if (indent == null)
            {
                indent = string.Empty;
            }
            else if (indent.Length > 0)
            {
                sb.AppendFormat("{0}Inner ", indent);
            }

            sb.AppendFormat("Exception Found:\n{0}Type: {1}", indent, e.GetType().FullName);
            sb.AppendFormat("\n{0}Message: {1}", indent, e.Message);
            sb.AppendFormat("\n{0}Source: {1}", indent, e.Source);
            sb.AppendFormat("\n{0}Stacktrace: {1}", indent, e.StackTrace);

            if (e.InnerException != null)
            {
                sb.Append("\n");
                CreateExceptionString(sb, e.InnerException, indent + "  ");
            }
        }

        public static string CaptchaDialog(byte[] captchaImg)
        {
            Form form = new Form();
            PictureBox pictureCaptcha = new System.Windows.Forms.PictureBox();
            TextBox textCaptcha = new System.Windows.Forms.TextBox();
            Button button1 = new System.Windows.Forms.Button();            

            pictureCaptcha.Location = new System.Drawing.Point(13, 13);
            pictureCaptcha.Name = "pictureCaptcha";
            pictureCaptcha.Size = new System.Drawing.Size(300, 57);
            pictureCaptcha.TabIndex = 0;
            pictureCaptcha.TabStop = false;
            
            
            textCaptcha.Location = new System.Drawing.Point(13, 78);
            textCaptcha.Name = "textCaptcha";
            textCaptcha.Size = new System.Drawing.Size(219, 20);
            textCaptcha.TabIndex = 1;
                        
            button1.Location = new System.Drawing.Point(239, 78);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(74, 23);
            button1.TabIndex = 2;
            button1.Text = "Submit";
            button1.UseVisualStyleBackColor = true;
            button1.DialogResult = DialogResult.OK;
            
            // CaptchaInputBox
            // 
            form.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            form.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            form.ClientSize = new System.Drawing.Size(324, 110);
            form.Controls.Add(button1);
            form.Controls.Add(textCaptcha);
            form.Controls.Add(pictureCaptcha);
            form.Name = "CaptchaInputBox";
            form.Text = "Please input the captcha";
            form.AcceptButton = button1;

            using (var ms = new MemoryStream(captchaImg))
            {
                pictureCaptcha.Image = Image.FromStream(ms);
            }

            DialogResult dialogResult = form.ShowDialog();
            return textCaptcha.Text;
        }

        public static List<string> useragents = new List<string>()
        {            
            "Mozilla/4.0 (Compatible; Windows NT 5.1; MSIE 6.0) (compatible; MSIE 6.0; Windows NT 5.1; NET CLR 1.1.4322; .NET CLR 2.0.50727)",
            "Mozilla/5.0 (X11; Linux x86_64; rv:2.2a1pre) Gecko/20110324 Firefox/4.2a1pre",
            "Mozilla/5.0 (X11; Linux x86_64; rv:2.2a1pre) Gecko/20100101 Firefox/4.2a1pre",
            "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:2.2a1pre) Gecko/20110324 Firefox/4.2a1pre",
            "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:2.2a1pre) Gecko/20110323 Firefox/4.2a1pre",
            "Mozilla/5.0 (X11; U; Linux i686; pl-PL; rv:1.9.0.2) Gecko/20121223 Ubuntu/9.25 (jaunty) Firefox/3.8",
            "Mozilla/5.0 (X11; U; Linux i686; pl-PL; rv:1.9.0.2) Gecko/2008092313 Ubuntu/9.25 (jaunty) Firefox/3.8",
            "Mozilla/5.0 (X11; U; Linux i686; it-IT; rv:1.9.0.2) Gecko/2008092313 Ubuntu/9.25 (jaunty) Firefox/3.8",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.9.2.3) Gecko/20100401 Mozilla/5.0 (X11; U; Linux i686; it-IT; rv:1.9.0.2) Gecko/2008092313 Ubuntu/9.25 (jaunty) Firefox/3.8",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; zh-CN; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; pt-BR; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8 GTB7.1",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; it; rv:1.9.2.8) Gecko/20100722 AskTbADAP/3.9.1.14019 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; he; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; fr; rv:1.9.2.8) Gecko/20100722 Firefox 3.6.8 GTB7.1",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-GB; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8 ( .NET CLR 3.5.30729; .NET4.0C)",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; de; rv:1.9.2.8) Gecko/20100722 Firefox 3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; de; rv:1.9.2.3) Gecko/20121221 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 5.2; zh-TW; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; zh-CN; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; tr; rv:1.9.2.8) Gecko/20100722 Firefox/3.6.8 ( .NET CLR 3.5.30729; .NET4.0E)",
            "Mozilla/5.0 (X11; U; Linux x86_64; en-US; rv:1.9.2.6) Gecko/20100628 Ubuntu/10.04 (lucid) Firefox/3.6.6",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; pt-PT; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; it; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6 ( .NET CLR 3.5.30729)",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6 (.NET CLR 3.5.30729)",
            "Mozilla/5.0 (Windows; U; Windows NT 6.0; zh-CN; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6 GTB7.1",
            "Mozilla/5.0 (Windows; U; Windows NT 6.0; nl; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; it; rv:1.9.2.6) Gecko/20100625 Firefox/3.6.6 ( .NET CLR 3.5.30729; .NET4.0E)",
            "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 5.2; Trident/4.0; Media Center PC 4.0; SLCC1; .NET CLR 3.0.04320)",
            "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0; SLCC1; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729; .NET CLR 1.1.4322)",
            "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0; InfoPath.2; SLCC1; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729; .NET CLR 2.0.50727)",
            "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 5.1; Trident/4.0; .NET CLR 1.1.4322; .NET CLR 2.0.50727)",
            "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 5.0; Trident/4.0; InfoPath.1; SV1; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729; .NET CLR 3.0.04506.30)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 5.0; Trident/4.0; FBSMTWB; .NET CLR 2.0.34861; .NET CLR 3.0.3746.3218; .NET CLR 3.5.33652; msn OptimizedIE8;ENUS)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.2; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; Media Center PC 6.0; InfoPath.2; MS-RTC LM 8)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; Media Center PC 6.0; .NET CLR 3.5.30729; .NET CLR 3.0.30729; .NET4.0C)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; InfoPath.3; .NET4.0C; .NET4.0E; .NET CLR 3.5.30729; .NET CLR 3.0.30729; MS-RTC LM 8)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; InfoPath.2)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; Zune 3.0)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; msn OptimizedIE8;ZHCN)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; MS-RTC LM 8; InfoPath.3; .NET4.0C; .NET4.0E) chromeframe/8.0.552.224",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; MS-RTC LM 8; .NET4.0C; .NET4.0E; Zune 4.7; InfoPath.3)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; MS-RTC LM 8; .NET4.0C; .NET4.0E; Zune 4.7)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; MS-RTC LM 8)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; InfoPath.3; Zune 4.0)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; InfoPath.3; .NET4.0C; .NET4.0E; MS-RTC LM 8; Zune 4.7)",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; InfoPath.3)",
            "Mozilla/5.0 (Windows; U; MSIE 7.0; Windows NT 6.0; en-US)",
            "Mozilla/5.0 (Windows; U; MSIE 7.0; Windows NT 6.0; el-GR)",
            "Mozilla/5.0 (MSIE 7.0; Macintosh; U; SunOS; X11; gu; SV1; InfoPath.2; .NET CLR 3.0.04506.30; .NET CLR 3.0.04506.648)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 6.0; WOW64; SLCC1; .NET CLR 2.0.50727; Media Center PC 5.0; c .NET CLR 3.0.04506; .NET CLR 3.5.30707; InfoPath.1; el-GR)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 6.0; SLCC1; .NET CLR 2.0.50727; Media Center PC 5.0; c .NET CLR 3.0.04506; .NET CLR 3.5.30707; InfoPath.1; el-GR)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 6.0; fr-FR)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 6.0; en-US)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows NT 5.2; WOW64; .NET CLR 2.0.50727)",
            "Mozilla/5.0 (compatible; MSIE 7.0; Windows 98; SpamBlockerUtility 6.3.91; SpamBlockerUtility 6.2.91; .NET CLR 4.1.89;GB)",
            "Mozilla/4.79 [en] (compatible; MSIE 7.0; Windows NT 5.0; .NET CLR 2.0.50727; InfoPath.2; .NET CLR 1.1.4322; .NET CLR 3.0.04506.30; .NET CLR 3.0.04506.648)",
            "Mozilla/4.0 (Windows; MSIE 7.0; Windows NT 5.1; SV1; .NET CLR 2.0.50727)",
            "Mozilla/4.0 (Mozilla/4.0; MSIE 7.0; Windows NT 5.1; FDM; SV1; .NET CLR 3.0.04506.30)",
            "Mozilla/4.0 (Mozilla/4.0; MSIE 7.0; Windows NT 5.1; FDM; SV1)",
            "Mozilla/4.0 (compatible;MSIE 7.0;Windows NT 6.0)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; SLCC2; .NET CLR 2.0.50727; InfoPath.3; .NET4.0C; .NET4.0E; .NET CLR 3.5.30729; .NET CLR 3.0.30729; MS-RTC LM 8)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; MS-RTC LM 8; .NET4.0C; .NET4.0E; InfoPath.3)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; .NET4.0C; .NET4.0E)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0;)",
            "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.0; YPC 3.2.0; SLCC1; .NET CLR 2.0.50727; Media Center PC 5.0; InfoPath.2; .NET CLR 3.5.30729; .NET CLR 3.0.30618)",
            "Mozilla/5.0 (X11; U; Linux x86_64; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Ubuntu/10.10 Chromium/10.0.648.133 Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (X11; U; Linux x86_64; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (X11; U; Linux i686; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Ubuntu/10.10 Chromium/10.0.648.133 Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (X11; U; Linux i686; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_6_3; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (Macintosh; U; Intel Mac OS X 10_6_2; en-US) AppleWebKit/534.16 (KHTML, like Gecko) Chrome/10.0.648.133 Safari/534.16",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/534.19 (KHTML, like Gecko) Chrome/11.0.661.0 Safari/534.19",
            "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/534.18 (KHTML, like Gecko) Chrome/11.0.661.0 Safari/534.18",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/534.24 (KHTML, like Gecko) Chrome/11.0.696.3 Safari/534.24",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/534.24 (KHTML, like Gecko) Chrome/11.0.696.3 Safari/534.24",
            "Mozilla/5.0 (Windows NT 6.0) AppleWebKit/534.24 (KHTML, like Gecko) Chrome/11.0.696.3 Safari/534.24",
            "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/534.25 (KHTML, like Gecko) Chrome/12.0.706.0 Safari/534.25",
            "Opera/9.80 (X11; Linux x86_64; U; Ubuntu/10.10 (maverick); pl) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (X11; Linux i686; U; ja) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (X11; Linux i686; U; fr) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.1; U; zh-tw) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.1; U; zh-cn) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.1; U; sv) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.1; U; en-US) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.1; U; cs) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 6.0; U; pl) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 5.2; U; ru) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 5.1; U;) Presto/2.7.62 Version/11.01",
            "Opera/9.80 (Windows NT 5.1; U; cs) Presto/2.7.62 Version/11.01",
            "Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US; rv:1.9.2.13) Gecko/20101213 Opera/9.80 (Windows NT 6.1; U; zh-tw) Presto/2.7.62 Version/11.01",
            "Mozilla/5.0 (Windows NT 6.1; U; nl; rv:1.9.1.6) Gecko/20091201 Firefox/3.5.6 Opera 11.01",
            "Mozilla/5.0 (Windows NT 6.1; U; de; rv:1.9.1.6) Gecko/20091201 Firefox/3.5.6 Opera 11.01",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; de) Opera 11.01",
            "Opera/9.80 (X11; Linux x86_64; U; pl) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (X11; Linux i686; U; it) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.1; U; zh-cn) Presto/2.6.37 Version/11.00",
            "Opera/9.80 (Windows NT 6.1; U; pl) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.1; U; ko) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.1; U; fi) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.1; U; en-GB) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.1 x64; U; en) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.7.39 Version/11.00",
            "Opera/9.80 (Windows NT 5.1; U; ru) Presto/2.7.39 Version/11.00",
            "Opera/9.80 (Windows NT 5.1; U; MRA 5.5 (build 02842); ru) Presto/2.7.62 Version/11.00",
            "Opera/9.80 (Windows NT 5.1; U; it) Presto/2.7.62 Version/11.00",
            "Mozilla/5.0 (Windows NT 6.0; U; ja; rv:1.9.1.6) Gecko/20091201 Firefox/3.5.6 Opera 11.00",
            "Mozilla/5.0 (Windows NT 5.1; U; pl; rv:1.9.1.6) Gecko/20091201 Firefox/3.5.6 Opera 11.00",
            "Mozilla/5.0 (Windows NT 5.1; U; de; rv:1.9.1.6) Gecko/20091201 Firefox/3.5.6 Opera 11.00",
            "Mozilla/4.0 (compatible; MSIE 8.0; X11; Linux x86_64; pl) Opera 11.00",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; fr) Opera 11.00",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.0; ja) Opera 11.00",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.0; en) Opera 11.00",
            "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; pl) Opera 11.00",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.8.99 Version/11.10",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.8.99 Version/11.10",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.8.99 Version/11.10",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.8.99 Version/11.10",
            "Opera/9.80 (Windows NT 6.0; U; en) Presto/2.8.99 Version/11.10"            
        };

        public static Dictionary<int, string> industries = new Dictionary<int, string>
        {
	        {1, "Food"},
            {2, "Weapon"},
            {3, "Moving ticket"},
            {4, "House"},
            {7, "Food raw"},
            {12, "Weapon raw"}
        };

        public static Dictionary<int, string> countries = new Dictionary<int, string>
        {
            {167, "Albania"},
            {27, "Argentina"},
            {50, "Australia"},
            {33, "Austria"},
            {83, "Belarus"},
            {32, "Belgium"},
            {76, "Bolivia"},
            {69, "Bosnia and Herzegovina"},
            {9, "Brazil"},
            {42, "Bulgaria"},
            {23, "Canada"},
            {64, "Chile"},
            {14, "China"},
            {78, "Colombia"},
            {63, "Croatia"},
            {82, "Cyprus"},
            {34, "Czech Republic"},
            {55, "Denmark"},
            {165, "Egypt"},
            {70, "Estonia"},
            {39, "Finland"},
            {11, "France"},
            {12, "Germany"},
            {44, "Greece"},
            {13, "Hungary"},
            {48, "India"},
            {49, "Indonesia"},
            {56, "Iran"},
            {54, "Ireland"},
            {58, "Israel"},
            {10, "Italy"},
            {45, "Japan"},
            {71, "Latvia"},
            {72, "Lithuania"},
            {66, "Malaysia"},
            {26, "Mexico"},
            {80, "Montenegro"},
            {31, "Netherlands"},
            {84, "New Zealand"},
            {73, "North Korea"},
            {37, "Norway"},
            {57, "Pakistan"},
            {75, "Paraguay"},
            {77, "Peru"},
            {67, "Philippines"},
            {35, "Poland"},
            {53, "Portugal"},
            {81, "Republic of China (Taiwan)"},
            {79, "Republic of Macedonia (FYROM)"},
            {52, "Republic of Moldova"},
            {1, "Romania"},
            {41, "Russia"},
            {164, "Saudi Arabia"},
            {65, "Serbia"},
            {68, "Singapore"},
            {36, "Slovakia"},
            {61, "Slovenia"},
            {51, "South Africa"},
            {47, "South Korea"},
            {15, "Spain"},
            {38, "Sweden"},
            {30, "Switzerland"},
            {59, "Thailand"},
            {43, "Turkey"},
            {24, "USA"},
            {40, "Ukraine"},
            {166, "United Arab Emirates"},
            {29, "United Kingdom"},
            {74, "Uruguay"},
            {28, "Venezuela"}
        };

        public static Dictionary<int, string> currencies = new Dictionary<int, string>
        {
            {166, "AED"},
            {27, "ARS"},
            {33, "ATS"},
            {50, "AUD"},
            {69, "BAM"},
            {32, "BEF"},
            {42, "BGN"},
            {76, "BOB"},
            {9, "BRL"},
            {83, "BYR"},
            {23, "CAD"},
            {30, "CHF"},
            {64, "CLP"},
            {14, "CNY"},
            {78, "COP"},
            {82, "CYP"},
            {34, "CZK"},
            {12, "DEM"},
            {55, "DKK"},
            {70, "EEK"},
            {165, "EGP"},
            {15, "ESP"},
            {39, "FIM"},
            {11, "FRF"},
            {29, "GBP"},
            {62, "GOLD"},
            {44, "GRD"},
            {63, "HRK"},
            {13, "HUF"},
            {49, "IDR"},
            {54, "IEP"},
            {48, "INR"},
            {56, "IRR"},
            {10, "ITL"},
            {45, "JPY"},
            {73, "KPW"},
            {47, "KRW"},
            {72, "LTL"},
            {71, "LVL"},
            {52, "MDL"},
            {80, "MEP"},
            {79, "MKD"},
            {26, "MXN"},
            {66, "MYR"},
            {58, "NIS"},
            {31, "NLG"},
            {37, "NOK"},
            {84, "NZD"},
            {77, "PEN"},
            {67, "PHP"},
            {57, "PKR"},
            {35, "PLN"},
            {53, "PTE"},
            {75, "PYG"},
            {1, "RON"},
            {65, "RSD"},
            {41, "RUB"},
            {164, "SAR"},
            {38, "SEK"},
            {68, "SGD"},
            {61, "SIT"},
            {36, "SKK"},
            {59, "THB"},
            {43, "TRY"},
            {81, "TWD"},
            {40, "UAH"},
            {24, "USD"},
            {74, "UYU"},
            {28, "VEB"},
            {51, "ZAR"}            
        };


    }


}
