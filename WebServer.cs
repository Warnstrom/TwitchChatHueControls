using System.Net;

    public class Authorization
    {
        public string Code { get; }
        
        public Authorization(string code)
        {
            Code = code;
        }
    }

    public class WebServer
    {
        private HttpListener listener;

        public WebServer(string uri)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(uri);
        }

        public async Task<Authorization> Listen()
        {
            listener.Start();
            return await OnRequest();
        }

        private async Task<Authorization> OnRequest()
        {
            while(listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                var req = ctx.Request;
                var resp = ctx.Response;

                using (var writer = new StreamWriter(resp.OutputStream))
                {
                    if (req.QueryString.AllKeys.Any("code".Contains))
                    {
                        writer.WriteLine("Authorization successful! Check your application!");
                        writer.Flush();
                        return new Authorization(req.QueryString["code"]);
                    }
                    else
                    {
                        writer.WriteLine("No code found in query string!");
                        writer.Flush();
                    }
                }
            }
            return null;
        }
    }
