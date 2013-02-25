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
        #region privates
        private string Hash(FileInfo fileInfo)
        {
            SHA1Managed sha1 = new SHA1Managed();
            FileStream inFile = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            byte[] hashBytes = sha1.ComputeHash(inFile);
            string hash = string.Concat((from b in hashBytes
                                         select string.Format("{0:X2}", b)).ToArray());
            return hash;
        }
        private bool ValidFileName(string fileName)
        {
            string pattern = @"^\d{8}(\-\-\w+\-\-[^\-]+)+\-\-SVC.*\.PDF$";
            Regex r = new Regex(pattern);
            Match m = r.Match(fileName);
            return m.Success;
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
        #endregion
        #region adapter
        public Hashtable AddActivities(XElement root)
        {
            Hashtable result = new Hashtable();
            Db.Db db = Db.Db.Instance();
            try 
            {
                List<Hashtable> request = new List<Hashtable>();
                var activitiesElement = root.XPathSelectElement("//activities");
                foreach (var activityElement in activitiesElement.Elements())
                {
                    Hashtable item = new Hashtable();
                    request.Add(item);
                    item["path"] = activityElement.XPathSelectElement("//path").Value;
                    string s = activityElement.XPathSelectElement("//specialty").Value;
                    item["specialty"] = activityElement.XPathSelectElement("//specialty").Value.SpecialtyTrim();
                    item["subspecialty"] = activityElement.XPathSelectElement("//subspecialty").Value.SpecialtyTrim();
                    item["firstName"] = activityElement.XPathSelectElement("//firstname").Value;
                    item["lastName"] = activityElement.XPathSelectElement("//lastname").Value;
                }
                return db.AddActivities(request);
            }
            catch (Exception)
            {
                
                throw;
            }
            return result;
        }
        public Hashtable AddActivities_(XElement root)
        {
            try
            {
                int doctor = int.Parse(root.XPathSelectElement("//doctor").Value);
                int patient = int.Parse(root.XPathSelectElement("//patient").Value);
                var activityElements = root.XPathSelectElements("//activities");
                var activities = new Db.activity[activityElements.Count()];
                FileInfo[] files = new FileInfo[activityElements.Count()];
                int i = -1;
                foreach (var activityElement in activityElements)
                {
                    i++;

                    Db.activity activity = new Db.activity();
                    activity.location = "";
                    activity.specialty_id = int.Parse(activityElement.XPathSelectElement("//specialty").Value);
                    activity.location = activityElement.XPathSelectElement("//location").Value;
                    activity.procedure = activityElement.XPathSelectElement("//procedure").Value;
                    activity.date = DateTime.Parse(activityElement.XPathSelectElement("//date").Value);
                    activity.doctor_id = doctor;
                    activity.patient_id = patient;
                    activities[i] = activity;
                    files[i] = new FileInfo(activityElement.XPathSelectElement("//file").Value);
                }
                Db.Db db = Db.Db.Instance();
                return db.AddActivities(activities, files);
            }
            catch (Exception)
            {
                Hashtable result = new Hashtable();
                result["status"] = "error";
                result["reason"] = "AddActivities() failed";
                return result;
            }
        }
        public Hashtable UploadFile(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Db.Db db = Db.Db.Instance();
                string fullName = root.XPathSelectElement("//fullname").Value;
                FileInfo fi = new FileInfo(fullName);
                db.AddFile(fi);
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "UploadFile failed";
            }
            return result;
        }
        public Hashtable GetPatients()
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Db.Db db = Db.Db.Instance();
                result["patients"] = db.Patients();
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "Could not get patients";
            }
            return result;
        }
        public Hashtable GetPeopleOnDisk()
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
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
                result["people"] = people;
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "Could not get people on disk";
            }
            return result;
        }
        public Hashtable AddPatient(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string firstName = root.XPathSelectElement("//firstName").Value;
                string lastName = root.XPathSelectElement("//lastName").Value;
                string dateOfBirth = root.XPathSelectElement("//dateOfBirth").Value;
                string gender = root.XPathSelectElement("//gender").Value;
                string emergencyContact = root.XPathSelectElement("//emergencyContact").Value;
                Db.Db db = Db.Db.Instance();
                db.AddPatient(firstName, lastName, dateOfBirth, gender, emergencyContact);
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "AddPatient failed";
            }
            return result;
        }
        public Hashtable AddDoctor(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
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
                Db.Db db = Db.Db.Instance();
                db.AddDoctor(firstName, lastName, shortName, address1, address2, address3, city, locality1, locality2, postalCode, country, voice, fax, email, contact);
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "AddDoctor failed";
            }
            return result;

        }
        public Hashtable GetFilesOnDisk(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";

            try
            {
                string firstName = root.XPathSelectElement("//firstname").Value;
                string lastName = root.XPathSelectElement("//lastname").Value;
                List<Dictionary<string, string>> results = new List<Dictionary<string, string>>();
                result["files"] = results;
                DirectoryInfo diConcierge = new DirectoryInfo(Properties.Settings.Default.Concierge);

                var person = diConcierge.GetDirectories(lastName + ", " + firstName)[0];

                string[] delim = { ", " };
                var specialties = (
                    from specialty in person.GetDirectories()
                    where Char.IsLetter(specialty.Name.ToCharArray()[0])
                    select specialty);
                foreach (var specialty in specialties)
                {
                    foreach (var specialtyPdf in specialty.GetFiles("*.pdf"))
                    {
                        if (!ValidFileName(specialtyPdf.Name))
                            continue;
                        Dictionary<string, string> entry = new Dictionary<string, string>();
                            Populate(entry, firstName, lastName, Hash(specialtyPdf), specialty.Name, "", specialtyPdf.Name,specialtyPdf.FullName);
                        results.Add(entry);
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
                            Populate(entry, firstName, lastName, Hash(subspecialtyPdf), specialty.Name, subspecialty.Name, subspecialtyPdf.Name, subspecialtyPdf.FullName);
                            results.Add(entry);
                        }
                    }
                }
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "Could not get concierge document(s)";
            }
            return result;
        }
        #endregion
    } // objectForScripting
} // namespace Concierge_Manager
