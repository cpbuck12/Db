using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HttpServer;
using HttpServer.Sessions;
using System.Net;
using System.Windows.Forms;

namespace Concierge_Manager
{
#if NOWHERE
    public class SimpleModule : HttpServer.HttpModules.HttpModule
    {
        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {
            /*
            Console.WriteLine(request.QueryString["id"].Value + " got request");
            response.Status = HttpStatusCode.OK;

            byte[] buffer = Encoding.ASCII.GetBytes(request.QueryString["id"].Value);
            response.Body.Write(buffer, 0, buffer.Length);
*/
            return true;
        }
    }
#endif
    public class AjaxObjectForScripting : HttpServer.HttpModules.HttpModule
    {
        ObjectForScripting objectForScripting;
        public AjaxObjectForScripting(ObjectForScripting objectForScripting)
        {
            this.objectForScripting = objectForScripting;
        }
        public void AjaxReply(string data, IHttpResponse response)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            response.Body.Write(buffer, 0, buffer.Length);
            response.Status = HttpStatusCode.OK;
            response.ContentType = "application/json";
            response.Status = HttpStatusCode.OK;
        }
        public bool GetFile(string[] parts, IHttpResponse response)
        {
            string basePath;
            if (parts.Length == 1 && parts[0] == "work.htm")
            {
                basePath = @"..\..\web\public";
            }
            else
            {
                basePath = @"C:\Users\pacmny_local\git\concierge\public";
//                basePath = @"C:\temp";
            }
            foreach (string part in parts)
                basePath = basePath + "\\" + part;
            FileStream fs = null;
            try
            {
                fs = new FileStream(basePath, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)(fs.Length));
                response.Body.Write(buffer, 0, (int)(fs.Length));
                response.Status = HttpStatusCode.OK;
                if (Regex.IsMatch(basePath.ToLower(), @".*\.css$"))
                {
                    response.ContentType = "text/css";
                }
                else if (Regex.IsMatch(basePath.ToLower(), @".*\.js$"))
                {
                    response.ContentType = "text/javascript";
                }
                else if (Regex.IsMatch(basePath.ToLower(), @".*\.png$"))
                {
                    response.ContentType = "image/png";
                }
                response.Status = HttpStatusCode.OK;
            }
            catch (Exception)
            {
                if(fs != null)
                    fs.Dispose();
                response.Status = HttpStatusCode.NotFound;
                return false;
            }
            if (fs != null)
                fs.Dispose();
            return true;
        }
        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {
            //MessageBox.Show(request.Uri.ToString());
            if (request.UriParts.Length == 0)
            {
                string[] files = { "work.htm" };
                return GetFile(files, response);
            }
            else if (request.UriParts[0].ToLower() == "ajax")
            {
                switch (request.UriParts[1])
                {
                    case "GetPeopleOnDisk":
                        AjaxReply(objectForScripting.GetPeopleOnDisk(), response);
                        return true;
                    case "GetFilesOnDisk":
                        string firstName = request.Param["FirstName"].Value;
                        string lastName = request.Param["LastName"].Value;
                        AjaxReply(objectForScripting.GetFilesOnDisk(firstName, lastName), response);
                        return true;
                    case "GetPeopleInDb":
                        {
                            string patients = objectForScripting.GetPatients();
                            AjaxReply(patients, response);
                            return true;
                        }
                }
                return false; // TODO finish this
            }
            else
            {
                return GetFile(request.UriParts, response);
            }
        }
    }
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [ComVisible(true)]
    public class ObjectForScripting
    {
        HttpServer.HttpServer httpServer;
        AjaxObjectForScripting ajaxObjectForScripting;
        public ObjectForScripting()
        {
            httpServer = new HttpServer.HttpServer();
            ajaxObjectForScripting = new AjaxObjectForScripting(this);
            httpServer.Add(ajaxObjectForScripting);
            httpServer.Start(IPAddress.Any, 50505);
            httpServer.BackLog = 5;
        }
        public string GetPatients()
        {
            //            throw new Exception("Yabba Dabba Doooo!!!");
            Db.Db db = Db.Db.Instance();
            Db.patient[] patients = db.Patients();
            string s = (new JavaScriptSerializer()).Serialize(patients);
            return s;
        }
        private string Hash(FileInfo fileInfo)
        {
            SHA1Managed sha1 = new SHA1Managed();
            FileStream inFile = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            byte[] hashBytes = sha1.ComputeHash(inFile);
            string hash = string.Concat((from b in hashBytes
                                         select string.Format("{0:X2}", b)).ToArray());
            return hash;
        }
        bool ValidFileName(string fileName)
        {
            string pattern = @"^\d{8}(\-\-\w+\-\-[^\-]+)+\-\-SVC.*\.PDF$";
            Regex r = new Regex(pattern);
            Match m = r.Match(fileName);
            return m.Success;
        }
        private void Populate(Dictionary<string, string> entry, string firstName, string lastName, string hash, string specialty, string subspecialty, string fileName)
        {
            entry.Add("FirstName", firstName);
            entry.Add("LastName", lastName);
            entry.Add("Hash", hash);
            entry.Add("Specialty", specialty);
            entry.Add("Subspecialty", subspecialty);
            entry.Add("FileName", fileName);
        }
        public string GetPeopleOnDisk()
        {
            List<Dictionary<string, string>> people = new List<Dictionary<string, string>>();
            DirectoryInfo diConcierge = new DirectoryInfo(Properties.Settings.Default.Concierge);
            foreach (var person in diConcierge.GetDirectories("*, *"))
            {
                Dictionary<string, string> entry = new Dictionary<string, string>();
                string[] delim = { ", " };
                string[] names = person.Name.Split(delim, StringSplitOptions.None);
                entry.Add("FirstName", names[1]);
                entry.Add("LastName", names[0]);
                people.Add(entry);
            }
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Serialize(people);
        }
        public string GetFilesOnDisk(string firstName, string lastName)
        {
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            DirectoryInfo diConcierge = new DirectoryInfo(Properties.Settings.Default.Concierge);

            var person = diConcierge.GetDirectories(lastName + ", " + firstName)[0];

            string[] delim = { ", " };
            var specialties = (
                from specialty in person.GetDirectories()
                where Char.IsLetter(specialty.Name.ToCharArray()[0])
                select specialty);
            foreach (var specialty in specialties)
            {
                string hash;
                foreach (var specialtyPdf in specialty.GetFiles("*.pdf"))
                {
                    if(!ValidFileName(specialtyPdf.Name))
                        continue;
                    Dictionary<string, string> entry = new Dictionary<string, string>();
                        Populate(entry, firstName, lastName, Hash(specialtyPdf) , specialty.Name, "", specialtyPdf.Name);
                    result.Add(entry);
                }
                var subspecialties = (
                    from subspecialty in specialty.GetDirectories()
                    where Char.IsLetter(subspecialty.Name.ToCharArray()[0])
                    select subspecialty);
                foreach (var subspecialty in subspecialties)
                {
                    foreach (var subspecialtyPdf in subspecialty.GetFiles("*.pdf"))
                    {
                        if (!ValidFileName(subspecialtyPdf.Name))
                            continue;
                        Dictionary<string, string> entry = new Dictionary<string, string>();
                        Populate(entry, firstName, lastName, Hash(subspecialtyPdf), specialty.Name, subspecialty.Name, subspecialtyPdf.Name);
                        result.Add(entry);
                    }
                }
            }
            JavaScriptSerializer jss = new JavaScriptSerializer();
            return jss.Serialize(result);
        }
    }
}
