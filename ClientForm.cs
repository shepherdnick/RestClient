using System;
using System.IO;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace RestClient
{
    public partial class ClientForm : Form
    {
        #region Class variables

        HttpWebRequest _request = null;
        CookieContainer _cookieContainer = new CookieContainer();
        string _method = string.Empty;

        #endregion

        private void MakeRequest()
		{
			ResponseTextBox.Text = "Please wait...";

			var url = UrlTextBox.Text;
			_method = VerbComboBox.Text;
			var requestBody = RequestBodyTextBox.Text;

			try
			{
                // Create the request object
				_request = (HttpWebRequest)WebRequest.Create(url);
                //_request.Credentials = new NetworkCredential(txtUsername.Text, txtPassword.Text);
                //_request.Headers.Add(HttpRequestHeader.Authorization, string.Format("Basic {0}", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", txtUsername.Text, txtPassword.Text)))));
                //_request.PreAuthenticate = true;
                //_request.ContentLength = RequestBodyTextBox.Text.Length;
                _request.Method = _method;
                //_request.CookieContainer = _cookieContainer;

                // Customizable strings
                //_request.UserAgent = "This is a generic user-agent";
                //_request.Headers.Add("CUSTOM_HEADER", "HI, HOW ARE YOU?");
                _request.Headers.Add("Authorization", "Bearer 89EF8594E2EA6756BAC84D26D75F8");

                // Change the content type if we're using something other than GET
                if (_method != "GET")
                {
                    if (radioButton1.Checked)
                    {
                        _request.ContentType = "application/json";
                    }
                    else if (radioButton2.Checked)
                    {
                        _request.ContentType = "application/xml";
                    }
                    else if (radioButton3.Checked) { 
                        _request.ContentType = textBox3.Text;
                    }
                }
                
                // Set the request body to a block of data
				SetBody(_request, requestBody);

                try
                {
                    // Try and get a response, if we don't then just process the event
                    var response = (HttpWebResponse)_request.GetResponse();
                    ProcessResponse(response);
                }
                catch(WebException wex)
                {
                    // If we're doing digest auth, then we expect a 401 (Unauthorized)
                    if(wex.Response != null && ((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.Unauthorized)
                    {
                        ProcessResponse(wex.Response);
                    }
                    else
                    {
                        ResponseTextBox.Text += "ERROR: " + wex.Message;
                    }
                }
			}
			catch (Exception ex)
			{
				ResponseTextBox.Text += "ERROR: " + ex.Message;
			}
		}

        private void ProcessResponse(WebResponse response)
        {
            // Case to a better response objects
            HttpWebResponse upgradedResponse = (HttpWebResponse)response;

            // If we've received a 401, we're trying to do digest auth
            if (upgradedResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Get the authentication header which contains all the headers we need to reply with
                var wwwAuthenticateHeader = upgradedResponse.Headers["WWW-Authenticate"];

                // Turn the request uri into a uri object so we can use it later
                Uri uri = new Uri(_request.RequestUri.ToString());

                // Build the lookahead (?= regular expression (order doesn't matter)
                var regularExpression = "(?=.*realm=\"(?<realm>.*?)\") " + 
                    "(?=.*nonce=\"(?<nonce>.*?)\")" + 
                    "(?=.*opaque=\"(?<opaque>.*?)\")" + 
                    "(?=.*algorithm=(?<algorithm>.*?),)" + 
                    "(?=.*qop=\"(?<qop>.*?)\")";

                Match match = Regex.Match(wwwAuthenticateHeader, regularExpression);
                if (match.Success)
                {
                    // If we got all of those paramters from the header, grab them as variables
                    string realm = match.Groups["realm"].Value;
                    string nonce = match.Groups["nonce"].Value;
                    string opaque = match.Groups["opaque"].Value;
                    string qop = match.Groups["qop"].Value;
                    string algorithm = match.Groups["algorithm"].Value;

                    // Generate the encrypted authentication string
                    string authResp = GenerateAuthResponse(txtUsername.Text, realm, txtPassword.Text, _method.ToString(), uri.PathAndQuery, nonce);

                    // Build the auth string to use as a header for the second request
                    string authString = string.Format("Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", response=\"{4}\"",
                        txtUsername.Text, realm, nonce, uri.PathAndQuery, authResp);

                    // Create the second request
                    var secondRequest = (HttpWebRequest)WebRequest.Create(_request.RequestUri.ToString());
                    secondRequest.Headers.Add("Authorization", authString); // add our encrypted auth string
                    secondRequest.CookieContainer = _cookieContainer; // add the same bunch of cookies we sent with the original request

                    // Try a second request to authenticate this time
                    var secondResponse = (HttpWebResponse)secondRequest.GetResponse();
                    ResponseTextBox.Text = ConvertResponseToString(secondResponse);
                }
            }
            else if(upgradedResponse.StatusCode == HttpStatusCode.OK)
            {
                // If we just did a normal non-digest request then just process the response
                ResponseTextBox.Text = ConvertResponseToString(upgradedResponse);
            }
        }

        string GenerateAuthResponse(string user, string realm, string pass, string command, string uri, string nonce)
        {
            string hash1 = string.Empty;
            string hash2 = string.Empty;

            using (MD5 hashObject = MD5.Create())
            {
                hash1 = GetMd5Hash(hashObject, String.Format("{0}:{1}:{2}", user, realm, pass));
                hash2 = GetMd5Hash(hashObject, String.Format("{0}:{1}", command, uri));
                return GetMd5Hash(hashObject, string.Format("{0}:{1}:{2}", hash1, nonce, hash2));
            }
        }

        string GetMd5Hash(MD5 md5Hash, string input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

		void SetBody(HttpWebRequest request, string requestBody)
		{
			if (requestBody.Length > 0)
			{
                using (Stream requestStream = request.GetRequestStream())
                {
                    using (StreamWriter writer = new StreamWriter(requestStream))
                    {
                        writer.Write(requestBody);
                    }
                }
			}
		}

		string ConvertResponseToString(HttpWebResponse response)
		{
			string result = "Status code: " + (int)response.StatusCode + " " + response.StatusCode + "\r\n";

			foreach (string key in response.Headers.Keys)
			{
				result += string.Format("{0}: {1} \r\n", key, response.Headers[key]);
			}

			result += "\r\n";
			result += new StreamReader(response.GetResponseStream()).ReadToEnd();

			return result;
		}

		public ClientForm()
		{
			InitializeComponent();
		}

		void ClientForm_Load(object sender, EventArgs e)
		{
			VerbComboBox.SelectedIndex = 0;
			UrlTextBox.Text = "http://localhost/";
		}

		void ExecuteButton_Click(object sender, EventArgs e)
		{
			MakeRequest();
		}

		void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				MakeRequest();
				e.Handled = true;
			}
		}
	}
}
