﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cowboy
{
    public class Request : IDisposable
    {
        private readonly List<HttpFile> files = new List<HttpFile>();
        private dynamic form = new DynamicDictionary();
        private IDictionary<string, string> cookies;

        public Request(string method, string path, string scheme)
            : this(method, new Url { Path = path, Scheme = scheme })
        {
        }

        public Request(string method,
            Url url,
            RequestStream body = null,
            IDictionary<string, IEnumerable<string>> headers = null,
            string ip = null,
            byte[] certificate = null,
            string protocolVersion = null)
        {
            if (String.IsNullOrEmpty(method))
            {
                throw new ArgumentOutOfRangeException("method");
            }

            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            if (url.Path == null)
            {
                throw new ArgumentNullException("url.Path");
            }

            if (String.IsNullOrEmpty(url.Scheme))
            {
                throw new ArgumentOutOfRangeException("url.Scheme");
            }

            this.UserHostAddress = ip;

            this.Url = url;

            this.Method = method;

            this.Query = url.Query.AsQueryDictionary();

            this.Body = body ?? RequestStream.FromStream(new MemoryStream());

            this.Headers = new RequestHeaders(headers ?? new Dictionary<string, IEnumerable<string>>());

            //this.Session = new NullSessionProvider();

            if (certificate != null && certificate.Length != 0)
            {
                this.ClientCertificate = new X509Certificate2(certificate);
            }

            this.ProtocolVersion = protocolVersion ?? string.Empty;

            if (String.IsNullOrEmpty(this.Url.Path))
            {
                this.Url.Path = "/";
            }

            this.ParseFormData();
            this.RewriteMethod();
        }

        public X509Certificate ClientCertificate { get; private set; }
        public string ProtocolVersion { get; private set; }
        public string UserHostAddress { get; private set; }
        public string Method { get; private set; }
        public Url Url { get; private set; }
        public string Path
        {
            get
            {
                return this.Url.Path;
            }
        }
        public dynamic Query { get; set; }
        public RequestStream Body { get; private set; }
        public IDictionary<string, string> Cookies
        {
            get { return this.cookies ?? (this.cookies = this.GetCookieData()); }
        }
        //public ISession Session { get; set; }

        private IDictionary<string, string> GetCookieData()
        {
            var cookieDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            //if (!this.Headers.Cookie.Any())
            //{
            //    return cookieDictionary;
            //}

            var values = this.Headers["cookie"].First().TrimEnd(';').Split(';');
            foreach (var parts in values.Select(c => c.Split(new[] { '=' }, 2)))
            {
                var cookieName = parts[0].Trim();
                string cookieValue;

                if (parts.Length == 1)
                {
                    //Cookie attribute
                    cookieValue = string.Empty;
                }
                else
                {
                    cookieValue = parts[1];
                }

                cookieDictionary[cookieName] = cookieValue;
            }

            return cookieDictionary;
        }

        public IEnumerable<HttpFile> Files
        {
            get { return this.files; }
        }

        public dynamic Form
        {
            get { return this.form; }
        }

        public RequestHeaders Headers { get; private set; }

        public void Dispose()
        {
            ((IDisposable)this.Body).Dispose();
        }

        private void ParseFormData()
        {
            if (string.IsNullOrEmpty(this.Headers.ContentType))
            {
                return;
            }

            var contentType = this.Headers["content-type"].First();
            var mimeType = contentType.Split(';').First();
            if (mimeType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new StreamReader(this.Body);
                this.form = reader.ReadToEnd().AsQueryDictionary();
                this.Body.Position = 0;
            }

            if (!mimeType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var boundary = Regex.Match(contentType, @"boundary=""?(?<token>[^\n\;\"" ]*)").Groups["token"].Value;
            var multipart = new HttpMultipart(this.Body, boundary);

            var formValues = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            foreach (var httpMultipartBoundary in multipart.GetBoundaries())
            {
                if (string.IsNullOrEmpty(httpMultipartBoundary.Filename))
                {
                    var reader =
                        new StreamReader(httpMultipartBoundary.Value);
                    formValues.Add(httpMultipartBoundary.Name, reader.ReadToEnd());

                }
                else
                {
                    this.files.Add(new HttpFile(httpMultipartBoundary));
                }
            }

            foreach (var key in formValues.AllKeys.Where(key => key != null))
            {
                this.form[key] = formValues[key];
            }

            this.Body.Position = 0;
        }

        private void RewriteMethod()
        {
            if (!this.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var overrides =
                new List<Tuple<string, string>>
                {
                    Tuple.Create("_method form input element", (string)this.Form["_method"]),
                    Tuple.Create("X-HTTP-Method-Override form input element", (string)this.Form["X-HTTP-Method-Override"]),
                    Tuple.Create("X-HTTP-Method-Override header", this.Headers["X-HTTP-Method-Override"].FirstOrDefault())
                };

            var providedOverride =
                overrides.Where(x => !string.IsNullOrEmpty(x.Item2));

            if (!providedOverride.Any())
            {
                return;
            }

            if (providedOverride.Count() > 1)
            {
                var overrideSources =
                    string.Join(", ", providedOverride);

                var errorMessage =
                    string.Format("More than one HTTP method override was provided. The provided values where: {0}", overrideSources);

                throw new InvalidOperationException(errorMessage);
            }

            this.Method = providedOverride.Single().Item2;
        }
    }
}
