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
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Collections;

namespace Concierge_Manager
{
    public static class Utility
    {
        public static string SpecialtyTrim(this string specialty)
        {
            specialty = specialty.Trim();
            while (specialty.EndsWith("#") && specialty != string.Empty)
                specialty = specialty.Substring(0, specialty.Length).Trim();
            return specialty;
        }
    }
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
        public void AjaxReply(IList payload, IHttpResponse response)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            response.Body.Position = 0;
            string str = ser.Serialize(payload);
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            response.Body.Write(buffer, 0, buffer.Length);
            response.Status = HttpStatusCode.OK;
            response.ContentType = "applixation/text";
        }
        public void AjaxReply(Hashtable payload, IHttpResponse response)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            response.Body.Position = 0;
            string str = ser.Serialize(payload);
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            response.Body.Write(buffer, 0, buffer.Length);
            response.Status = HttpStatusCode.OK;
            response.ContentType = "applixation/text";
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
                //basePath = @"C:\work\concierge\public";
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
        private XElement GetXmlRequestPayload(IHttpRequest request)
        {
            request.Body.Position = 0;
            XmlReader reader = JsonReaderWriterFactory.CreateJsonReader(request.Body, new System.Xml.XmlDictionaryReaderQuotas());
            XElement root = XElement.Load(reader);
            return root;
        }
        private string GetRequestPayload(IHttpRequest request)
        {
//            byte[] payload = GetXmlRequestPayload(request);
//            string payloadText = Encoding.ASCII.GetString(payload);
//            return payloadText;
            throw new NotImplementedException("converting GetRequestPayload");
            return "uhoh";
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
                string payloadText;
                XElement xmlRequest;

                switch (request.UriParts[1])
                {
                    case "AddPatient":
                        xmlRequest = GetXmlRequestPayload(request);
                        AjaxReply(objectForScripting.AddPatient(xmlRequest), response);
                        return true;
                    case "AddDoctor":
                        xmlRequest = GetXmlRequestPayload(request);
                        AjaxReply(objectForScripting.AddDoctor(xmlRequest), response);
                        return true;
                    case "AddActivities":
                        {
                            xmlRequest = GetXmlRequestPayload(request);
                            AjaxReply(objectForScripting.AddActivities(xmlRequest), response);
                            return true;

                            payloadText = GetRequestPayload(request);
                            string[] lines = payloadText.Split(new string[] { "\n" }, StringSplitOptions.None);
                            int doctorID = int.Parse(lines[0]);
                            int patientID = int.Parse(lines[1]);
                            int cFiles = int.Parse(lines[2]);
                            Db.Activity[] files = new Db.Activity[cFiles];
                            for (int iFile = 0; iFile < cFiles; iFile++)
                            {
                                files[iFile] = new Db.Activity();
                                string specialty = lines[3 + 0 + iFile * 3];
                                string subspecialty = lines[3 + 1 + iFile * 3];
                                string fullName = lines[3 + 2 + iFile * 3];
                                files[iFile].specialty = specialty.SpecialtyTrim();
                                files[iFile].subspecialty = subspecialty.SpecialtyTrim();
                                files[iFile].fileInfo = new FileInfo(fullName);
                            }
                            AjaxReply(objectForScripting.AddActivities(doctorID,patientID,files),response);
                        }
                        return true;
                    case "UploadFile":
                        {
                            payloadText = GetRequestPayload(request);
                            string[] lines = payloadText.Split(new string[] { "\n" }, StringSplitOptions.None);
                            string fullName = lines[2];
                            AjaxReply(objectForScripting.UploadFile(fullName), response);
                            return true;
                        }
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
                            AjaxReply(objectForScripting.GetPatients(), response);
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
        public Hashtable AddActivities(XElement root)
        {
            // TODO: MAJOR REWRITE: more complex, like uploading files and stuff
            int doctor = int.Parse(root.XPathSelectElement("//doctor").Value);
            int patient = int.Parse(root.XPathSelectElement("//patient").Value);
            var activities = root.XPathSelectElements("//activities");
            foreach (var activity in activities)
            {
                int specialty = int.Parse(activity.XPathSelectElement("//specialty").Value);
                

                                string specialty = lines[3 + 0 + iFile * 3];
                                string subspecialty = lines[3 + 1 + iFile * 3];
                                string fullName = lines[3 + 2 + iFile * 3];
                                files[iFile].specialty = specialty.SpecialtyTrim();
                                files[iFile].subspecialty = subspecialty.SpecialtyTrim();
                                files[iFile].fileInfo = new FileInfo(fullName);

            }
            var result = new Hashtable();
            return result;
        }
        public Hashtable AddActivities(int doctorID, int patientID, Db.Activity[] activities)
        {
            Db.Db db = Db.Db.Instance();
            db.AddActivities(doctorID, patientID, activities);
            var result = new Hashtable();
            result["Status"] = "OK";
            return result;
        }
        public Hashtable UploadFile(string fullName)
        {
            Db.Db db = Db.Db.Instance();
            FileInfo fi = new FileInfo(fullName);
            db.AddFile(fi);
            var result = new Hashtable();
            result["Status"] = "OK";
            return result;
        }
        public Hashtable GetPatients()
        {
            //            throw new Exception("Yabba Dabba Doooo!!!");
            Db.Db db = Db.Db.Instance();
            Db.patient[] patients = db.Patients();
            var result = new Hashtable();
            result["patients"] = patients;
            return result;
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
        string RemovePounds(string folderName)
        {
            try
            {
                folderName = folderName.Trim();
                if (folderName == string.Empty)
                    return folderName;
                if (folderName.Last() == '#')
                {
                    folderName = folderName.Substring(0, folderName.Length - 1);
                    folderName = folderName.Trim();
                }
                return folderName;

            }
            catch (Exception)
            {
                
                throw;
            }
            return folderName;
        }
        private void Populate(Dictionary<string, string> entry, string firstName, string lastName, string hash, string specialty, string subspecialty, string fileName, string fullName)
        {
            entry.Add("FirstName", firstName);
            entry.Add("LastName", lastName);
            entry.Add("Hash", hash);
            entry.Add("Specialty", specialty);
            entry.Add("Subspecialty", subspecialty);
            entry.Add("FileName", fileName);
            entry.Add("FullName", fullName);
        }
        public Hashtable GetPeopleOnDisk()
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
            var result = new Hashtable();
            result["people"] = people;
            return result;
        }
        public string AddSpecialty(string payloadText)
        {
            string[] lines = payloadText.Split(new string[] { "\n" }, StringSplitOptions.None);
            Db.Db db = Db.Db.Instance();
            return "ok";
        }
        public Hashtable AddPatient(XElement root)
        {
            string firstName = root.XPathSelectElement("//firstName").Value;
            string lastName = root.XPathSelectElement("//lastName").Value;
            string dateOfBirth = root.XPathSelectElement("//dateOfBirth").Value;
            string gender = root.XPathSelectElement("//gender").Value;
            string emergencyContact = root.XPathSelectElement("//emergencyContact").Value;
            Db.Db db = Db.Db.Instance();
            db.AddPatient(firstName, lastName, dateOfBirth, gender, emergencyContact);
            var result = new Hashtable();
            result["Status"] = "OK";
            return result;
        }
        public Hashtable AddDoctor(XElement root)
        {
            //string[] lines = payloadText.Split(new string[] { "\n" }, StringSplitOptions.None);
            string firstName = string.Empty, lastName = string.Empty, shortName = string.Empty, address1 = string.Empty, address2 = string.Empty;
            string address3 = string.Empty, city = string.Empty, locality1 = string.Empty, locality2 = string.Empty, postalCode = string.Empty;
            string country = string.Empty, voice = string.Empty, fax = string.Empty, email = string.Empty, contact = string.Empty;
            firstName = root.XPathSelectElement("//firstname").Value;
            lastName = root.XPathSelectElement("//lastname").Value;
            shortName = root.XPathSelectElement("//shortname").Value;
            address1 = root.XPathSelectElement("//address1").Value;
            address2 = root.XPathSelectElement("//address2").Value;
            address3 = root.XPathSelectElement("//address3").Value;
            city = root.XPathSelectElement("//city").Value;
            locality1 = root.XPathSelectElement("//locality1").Value;
            locality2 = root.XPathSelectElement("//locality2").Value;
            postalCode = root.XPathSelectElement("//postalcode").Value;
            country = root.XPathSelectElement("//country").Value;
            voice = root.XPathSelectElement("//voice").Value;
            voice = root.XPathSelectElement("//fax").Value;
            email = root.XPathSelectElement("//email").Value;
            contact = root.XPathSelectElement("//contact").Value;
            /*
            for (int i = 0; i < lines.Length; i += 2)
            {
                switch (lines[i])
                {
                    case "firstname":
                        firstName = lines[i + 1].Trim();
                        continue;
                    case "lasstname":
                        lastName = lines[i + 1].Trim();
                        continue;
                    case "shortname":
                        shortName = lines[i + 1].Trim();
                        continue;
                    case "address1":
                        address1 = lines[i + 1].Trim();
                        continue;
                    case "address2":
                        address2 = lines[i + 1].Trim();
                        continue;
                    case "address3":
                        address3 = lines[i + 1].Trim();
                        continue;
                    case "city":
                        city = lines[i + 1].Trim();
                        continue;
                    case "locality1":
                        locality1 = lines[i + 1].Trim();
                        continue;
                    case "locality2":
                        locality2 = lines[i + 1].Trim();
                        continue;
                    case "postalcode":
                        postalCode = lines[i + 1].Trim();
                        continue;
                    case "country":
                        country = lines[i + 1].Trim();
                        continue;
                    case "voice":
                        voice = lines[i + 1].Trim();
                        continue;
                    case "fax":
                        fax = lines[i + 1].Trim();
                        continue;
                    case "email":
                        email = lines[i + 1].Trim();
                        continue;
                    case "contact":
                        contact = lines[i + 1].Trim();
                        continue;
                }
            }*/
            Db.Db db = Db.Db.Instance();
            db.AddDoctor(firstName, lastName, shortName, address1, address2, address3, city, locality1, locality2, postalCode, country, voice, fax, email, contact);

            var result = new Hashtable();
            result["status"] = "ok";
            return result;

        }
        public Hashtable GetFilesOnDisk(string firstName, string lastName)
        {
            List<Dictionary<string, string>> results = new List<Dictionary<string, string>>();
            DirectoryInfo diConcierge = new DirectoryInfo(Properties.Settings.Default.Concierge);

            var person = diConcierge.GetDirectories(lastName + ", " + firstName)[0];

            string[] delim = { ", " };
            var specialties = (
                from specialty in person.GetDirectories()
                where Char.IsLetter(specialty.Name.ToCharArray()[0])
                select specialty);
            foreach (var specialty in specialties)
            {
                try
                {
                    foreach (var specialtyPdf in specialty.GetFiles("*.pdf"))
                    {
                        if (!ValidFileName(specialtyPdf.Name))
                            continue;
                        Dictionary<string, string> entry = new Dictionary<string, string>();
                        try
                        {
                            Populate(entry, firstName, lastName, Hash(specialtyPdf), specialty.Name, "", specialtyPdf.Name,specialtyPdf.FullName);
                        }
                        catch (Exception)
                        {
                            
                            throw;
                        }
                        results.Add(entry);
                    }

                }
                catch (Exception)
                {
                    
                    throw;
                }
                var subspecialties = (
                    from subspecialty in specialty.GetDirectories()
                    where Char.IsLetter(subspecialty.Name.ToCharArray()[0])
                    select subspecialty);
                try
                {
                    foreach (var subspecialty in subspecialties)
                    {
                        foreach (var subspecialtyPdf in subspecialty.GetFiles("*.pdf"))
                        {
                            if (!ValidFileName(subspecialtyPdf.Name))
                                continue;
                            Dictionary<string, string> entry = new Dictionary<string, string>();
                            Populate(entry, firstName, lastName, Hash(subspecialtyPdf), specialty.Name, subspecialty.Name, subspecialtyPdf.Name, subspecialtyPdf.FullName);
                            results.Add(entry);
                        }
                    }

                }
                catch (Exception)
                {
                    
                    throw;
                }
            }
            var result = new Hashtable();
            result["files"] = results;
            return result;
        }
    }
}
