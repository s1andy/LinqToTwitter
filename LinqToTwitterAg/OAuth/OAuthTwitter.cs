﻿/***********************************************************
 * Credits:
 * 
 * Eran Sandler -
 * OAuthBase Class
 * 
 * http://oauth.googlecode.com/svn/code/csharp/
 * 
 * Shannon Whitley -
 * Example of how to use modified version of
 * Eran Sandler's OAuthBase class in C#
 * 
 * http://www.voiceoftech.com/swhitley/?p=681
 * 
 * Joe Mayo -
 * 
 * Modified 5/17/09
 ***********************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using System.Threading;

#if SILVERLIGHT
    using System.Windows.Browser;
#else
    using System.Web;
#endif

namespace LinqToTwitter
{
    /// <summary>
    /// helps perform OAuth Authorization for LINQ to Twitter
    /// </summary>
    public class OAuthTwitter : OAuthBase, IOAuthTwitter
    {
        /// <summary>
        /// user agent header sent to Twitter
        /// </summary>
        public string OAuthUserAgent { get; set; }

        #region Properties

        /// <summary>
        /// Consumer Key
        /// </summary>
        public string OAuthConsumerKey { get; set; }

        /// <summary>
        /// Consumer Secret
        /// </summary>
        public string OAuthConsumerSecret { get; set; }

        /// <summary>
        /// OAuth Token
        /// </summary>
        public string OAuthToken { get; set; }

        /// <summary>
        /// OAuth Verifier
        /// </summary>
        public string OAuthVerifier { get; set; }

        /// <summary>
        /// OAuth Token Secret
        /// </summary>
        public string OAuthTokenSecret { get; set; }

        #endregion

        /// <summary>
        /// Get the link to Twitter's authorization page for this application.
        /// </summary>
        /// <param name="readOnly">true for read-only, otherwise read/Write</param>
        /// <returns>The url with a valid request token, or a null string.</returns>
        public string AuthorizationLinkGet(string requestToken, string authorizeUrl, string callback, bool readOnly, bool forceLogin)
        {
            string ret = null;
            string response = oAuthWebRequest(HttpMethod.GET, requestToken, String.Empty, callback);
            if (response.Length > 0)
            {
                var prefixChar = "?";

                string oAuthToken =
                    (from nameValPair in response.Split('&')
                     let pair = nameValPair.Split('=')
                     where pair[0] == "oauth_token"
                     select pair[1])
                    .SingleOrDefault();

                if (oAuthToken != null)
                {
                    ret = authorizeUrl + "?oauth_token=" + oAuthToken;
                    prefixChar = "&";
                }

                if (readOnly)
                {
                    ret += prefixChar + "oauth_access_type=read";
                    prefixChar = "&";
                }

                if (forceLogin)
                {
                    ret += prefixChar + "force_login=true";
                }
            }
            return ret;
        }

        /// <summary>
        /// Exchange the request token for an access token.
        /// </summary>
        /// <param name="authToken">The oauth_token is supplied by Twitter's authorization page following the callback.</param>
        public void AccessTokenGet(string authToken, string verifier, string accessTokenUrl, string callback, out string screenName, out string userID)
        {
            this.OAuthToken = authToken;
            this.OAuthVerifier = verifier;
            screenName = string.Empty;
            userID = string.Empty;

            string response = oAuthWebRequest(HttpMethod.GET, accessTokenUrl, String.Empty, callback);

            if (response.Length > 0)
            {
                var qs =
                    (from nameValPair in response.Split('&')
                     let pair = nameValPair.Split('=')
                     select new
                     {
                         Key = pair[0],
                         Value = pair[1]
                     })
                    .ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value);

                if (qs["oauth_token"] != null)
                {
                    this.OAuthToken = qs["oauth_token"];
                }

                if (qs["oauth_token_secret"] != null)
                {
                    this.OAuthTokenSecret = qs["oauth_token_secret"];
                }

                if (qs["screen_name"] != null)
                {
                    screenName = qs["screen_name"];
                }

                if (qs["user_id"] != null)
                {
                    userID = qs["user_id"];
                }
            }
        }

        /// <summary>
        /// returns a query string for an OAuth request
        /// </summary>
        /// <param name="url">Twitter query</param>
        /// <returns>Query string with OAuth parameters</returns>
        public void GetOAuthQueryString(HttpMethod method, string url, string callback, out string outUrl, out string queryString)
        {
            Uri uri = new Uri(url);

            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            //Generate Signature
            string sig = this.GenerateSignature(uri,
                this.OAuthConsumerKey,
                this.OAuthConsumerSecret,
                this.OAuthToken,
                this.OAuthTokenSecret,
                this.OAuthVerifier,
                callback,
                method.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out queryString);

            queryString += "&oauth_signature=" + HttpUtility.UrlEncode(sig);
        }

        /// <summary>
        /// processes POST request parameters
        /// </summary>
        /// <param name="url">url of request, without query string</param>
        /// 
        public string GetOAuthQueryStringForPost(string url)
        {
            OAuthVerifier = null;

            string outUrl;
            string queryString;
            GetOAuthQueryString(HttpMethod.POST, url, string.Empty, out outUrl, out queryString);

            const int Key = 0;
            const int Value = 1;

            var headerItems =
                from param in queryString.Split('&')
                let keyValPair = param.Split('=')
                select
                    keyValPair[Key] +
                    "=\"" +
                    keyValPair[Value] +
                    "\"";

            return "OAuth " + string.Join(",", headerItems.ToArray());
        }

        /// <summary>
        /// Url Encodes for OAuth Authentication
        /// </summary>
        /// <param name="value">string to be encoded</param>
        /// <returns>UrlEncoded string</returns>
        public string TwitterParameterUrlEncode(string value)
        {
            string ReservedChars = @"`!@#$%^&*()_-+=.~,:;'?/|\[] ";
            string UnReservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

            var result = new StringBuilder();

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            foreach (var symbol in value)
            {
                if (UnReservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else if (ReservedChars.IndexOf(symbol) != -1)
                {
                    result.Append('%' + String.Format("{0:X2}", (int)symbol).ToUpper());
                }
                else
                {
                    var encoded = HttpUtility.UrlEncode(symbol.ToString()).ToUpper();

                    if (!string.IsNullOrEmpty(encoded))
                    {
                        result.Append(encoded);
                    }
                }
            }

            return result.ToString();
        }

        private string PrepareAuthHeader(string authHeader)
        {
            var encodedParams =
                string.Join(
                    ",",
                    (from param in authHeader.Split('&')
                     let args = param.Split('=')
                     where !args[0].Contains("realm")
                     select args[0] + "=\"" + args[1] + "\"")
                    .ToArray());

            return "OAuth " + encodedParams;
        }

        /// <summary>
        /// Submit a web request using oAuth.
        /// </summary>
        /// <param name="method">GET or POST</param>
        /// <param name="url">The full url, including the querystring.</param>
        /// <param name="postData">Data to post (querystring format)</param>
        /// <returns>The web server response.</returns>
        public string oAuthWebRequest(HttpMethod method, string url, string postData, string callback)
        {
            string outUrl = "";
            string querystring = "";
            string ret = "";

            //Setup postData for signing.
            //Add the postData to the querystring.
            if (method == HttpMethod.POST)
            {
                if (postData.Length > 0)
                {
                    var qs =
                        (from nameValPair in postData.Split('&')
                         let pair = nameValPair.Split('=')
                         select new
                         {
                             Key = pair[0],
                             Value = pair[1]
                         })
                        .ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value);

                    postData = "";
                    foreach (string key in qs.Keys)
                    {
                        if (postData.Length > 0)
                        {
                            postData += "&";
                        }
                        qs[key] = HttpUtility.UrlDecode(qs[key]);
                        qs[key] = this.UrlEncode(qs[key]);
                        postData += key + "=" + qs[key];

                    }
                    if (url.IndexOf("?") > 0)
                    {
                        url += "&";
                    }
                    else
                    {
                        url += "?";
                    }
                    url += postData;
                }
            }

            Uri uri = new Uri(url);

            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            //Generate Signature
            string sig = this.GenerateSignature(uri,
                this.OAuthConsumerKey,
                this.OAuthConsumerSecret,
                this.OAuthToken,
                this.OAuthTokenSecret,
                this.OAuthVerifier,
                TwitterParameterUrlEncode(callback),
                method.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out querystring);

            querystring += "&oauth_signature=" + HttpUtility.UrlEncode(sig);

            //Convert the querystring to postData
            if (method == HttpMethod.POST)
            {
                postData = querystring;
                querystring = "";
            }

            ret = WebRequest(method, outUrl, querystring, postData);

            return ret;
        }

        /// <summary>
        /// Web Request Wrapper
        /// </summary>
        /// <param name="method">Http Method</param>
        /// <param name="url">Full url to the web resource</param>
        /// <param name="postData">Data to post in querystring format</param>
        /// <returns>The web server response.</returns>
        public string WebRequest(HttpMethod method, string url, string authHeader, string postData)
        {
            HttpWebRequest webRequest = null;
            string responseData = "";

            webRequest = System.Net.WebRequest.Create(url) as HttpWebRequest;
            webRequest.Method = method.ToString();
#if !SILVERLIGHT
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.UserAgent = OAuthUserAgent; 
#endif
            webRequest.Headers[HttpRequestHeader.Authorization] = PrepareAuthHeader(authHeader);

            if (method == HttpMethod.POST)
            {
                webRequest.ContentType = "application/x-www-form-urlencoded";

                byte[] postDataBytes = Encoding.UTF8.GetBytes(postData);

#if SILVERLIGHT
                // TODO: work in progress
                webRequest.BeginGetRequestStream(
                    new AsyncCallback(
                        ar =>
                        {
                            using (var requestStream = webRequest.EndGetRequestStream(ar))
                            {
                                requestStream.Write(postDataBytes, 0, postDataBytes.Length);
                            }
                        }), null);
#else
                var resetEvent = new ManualResetEvent(initialState: false);

                webRequest.BeginGetRequestStream(
                    new AsyncCallback(
                        ar =>
                        {
                            using (var requestStream = webRequest.EndGetRequestStream(ar))
                            {
                                requestStream.Write(postDataBytes, 0, postDataBytes.Length);
                            }
                            resetEvent.Set();
                        }), null);

                resetEvent.WaitOne();
#endif
            }

#if !SILVERLIGHT
            responseData = WebResponseGet(webRequest); 
#endif

            webRequest = null;

            return responseData;
        }

        /// <summary>
        /// Process the web response.
        /// </summary>
        /// <param name="webRequest">The request object.</param>
        /// <returns>The response data.</returns>
        public string WebResponseGet(HttpWebRequest webRequest)
        {
            string responseData = "";

            var resetEvent = new ManualResetEvent(initialState: false);
            HttpWebResponse res = null;

            webRequest.BeginGetResponse(
                new AsyncCallback(
                    ar =>
                    {
                        res = webRequest.EndGetResponse(ar) as HttpWebResponse;
                        using (var respStream = res.GetResponseStream())
                        using (var respReader = new StreamReader(respStream))
                        {
                            responseData = respReader.ReadToEnd();
                        }
                        resetEvent.Set();
                    }), null);

            resetEvent.WaitOne();

            return responseData;
        }
    }
}
