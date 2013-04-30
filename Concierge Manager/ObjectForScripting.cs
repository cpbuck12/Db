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
using System.Reflection;
using System.Resources;
using Microsoft.VisualBasic;
using Ionic.Zip;

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
            catch (Exception ex)
            {
                
                throw;
            }
            return result;
        }
        /*
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
                    activity.location_name = "";
                    activity.specialty_id = int.Parse(activityElement.XPathSelectElement("//specialty").Value);
                    activity.location_name = activityElement.XPathSelectElement("//location_name").Value;
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
        */
        public Hashtable BrowseDocuments()
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Db.Db db = Db.Db.Instance();
                result["documents"] = db.BrowseDocuments();
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = ex.Message;
            }
            return result;
        }
        /*
        public Hashtable GetSpecialties()
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Db.Db db = Db.Db.Instance();
                result["specialties"] = db.Specialties();
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "Could not get patients";
            }
            return result;
        }
         * */
        public byte[] DownloadFile(int fileId)
        {
            Db.Db db = Db.Db.Instance();
            return db.DownloadFile(fileId);
        }
        public Hashtable GetDoctors()
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Db.Db db = Db.Db.Instance();
                result["doctors"] = db.Doctors();
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "Could not get patients:"+ex.Message;
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
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "AddPatient failed. Exception message:" + ex.Message;
            }
            return result;
        }
        public string Guid()
        {
            return "{FDA99B54-4F60-4345-8524-6F2B9DEA4845}";
        }
        private string ReplaceGuid(string original,string tag,string value)
        {
            return original.Replace(Guid()+tag,value);
        }
        string Zebra(int i)
        {
            if ((i / 2) > 0)
                return "odd";
            else
                return "even";
        }

        public Hashtable CreateWebsite(XElement root)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try 
	        {
                Db.Db db = Db.Db.Instance();
                string destination = root.XPathSelectElement("//destination").Value;
                int id = int.Parse(root.XPathSelectElement("//id").Value);
                Directory.CreateDirectory(destination + @"\info");
                Directory.CreateDirectory(destination + @"\info\activities");
                Stream libStream = GetResourceAsStream("library_zip");
                using (ZipFile zf = ZipFile.Read(libStream))
                {
                    foreach (ZipEntry e in zf)
                    {
                        e.Extract(destination+@"\info");
                    }
                }

                string index_htm = GetResource("index_htm");
                WriteFile(destination + @"\index.html", index_htm);
                Hashtable h = db.GetPatient(id);
                string main_htm = GetResource("main_htm");
                h =(Hashtable) h["data"];
                main_htm = ReplaceGuid(main_htm, "name", string.Format("{0},{1}", h["last"], h["first"]));
                object o = h["dob"];
                DateTime o_dt = (DateTime)o;
                main_htm = ReplaceGuid(main_htm, "dob", ((DateTime)h["dob"]).ToString());
                main_htm = ReplaceGuid(main_htm, "gender", h["gender"] as string);
                main_htm = ReplaceGuid(main_htm, "emergencycontact", h["emergency_contact"] as string);
                StringBuilder sb = new StringBuilder();
                int i = 0;

                Db.doctor[] alldoctors = db.GetDoctors(recentOnly: true, patient: id);
                sb.AppendLine("<table class='doctors'><thead><tr><th>Name</th><th>id</id></tr></thead><tbody>");
                foreach (var ddr in alldoctors)
                {
                    sb.AppendLine("<tr><td>" + ddr.lastname + "," + ddr.firstname + "</td><td>" + ddr.id.ToString() + "</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
                main_htm = ReplaceGuid(main_htm, "doctors", sb.ToString());
                sb.Clear();
                sb.AppendLine("<table><thead><tr><th>YYYY-MM-DD</th><th>Procedure</th><th>Specialty</th><th>Practitioner</th><th>Location</th><th>doctor_id</th><th>document_id</th></tr></thead><tbody>");
                Hashtable activities = db.GetActivities(recent: true, patient: id);
                List<Hashtable> activitiesList = activities["items"] as List<Hashtable>;
                foreach (Hashtable activity in activitiesList)
                {
                    DateTime dt = (DateTime)activity["binarydate"];
                    Db.doctor doctor = activity["doctor"] as Db.doctor;
                    sb.AppendLine(string.Format("<tr><td>{0}</td><td>{1}</td><td>{2}/{3}</td><td>{4},{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                        dt.ToString("yyyy-MM-dd"),
                        activity["procedure"],
                        activity["specialty"],
                        activity["subspecialty"],
                        doctor.lastname,
                        doctor.firstname,
                        activity["location_name"],
                        activity["doctorid"],
                        activity["documentid"]));
                }
                sb.AppendLine("</tbody></table>");
                main_htm = ReplaceGuid(main_htm, "allprocedures", sb.ToString());
                sb.Clear();
                foreach(var ddr in alldoctors)
                {
                    sb.AppendLine(string.Format("<a name='doctor{0}'/>", ddr.id));
                    sb.AppendLine(string.Format("<br /><h4>Practitioner:{0},{1}</h4><br />", ddr.lastname, ddr.firstname));
                    sb.AppendLine(string.Format("<table class='doctor{0}'><thead><tr><th>YYYY-MM-DD</th><th>Procedure</th><th>docment_id</th></tr></thead><tbody>", ddr.id));
                    Hashtable actsHashtable = db.GetActivities(recent: true, patient: id, doctor: ddr.id);
                    List<Hashtable> acts = actsHashtable["items"] as List<Hashtable>;
                    foreach (Hashtable act in acts)
                    {
                        DateTime dt = (DateTime)act["binarydate"];
                        string date = dt.ToString("yyyy-MM-dd");
                        sb.AppendLine(string.Format("<tr><td>{0}</td><td>{1}</td><td>{2}</td></tr>",
                            date,
                            act["procedure"],
                            act["documentid"]));
                    }
                    sb.AppendLine("</tbody></table><br />");
                    sb.AppendLine(string.Format("<input type='button' class='openreport{0}' value='Open Procedure Report' />", ddr.id));
                    sb.AppendLine("<input type='button' class='gotosummary' value='Go back to summary' /><br />");
                }
                main_htm = ReplaceGuid(main_htm, "doctordocuments", sb.ToString());
                WriteFile(destination + @"\info\main.html", main_htm);
                
                Db.detail_item[] details = db.GetDetails(patient: id);
                if (details == null)
                {
                    main_htm = ReplaceGuid(main_htm, "detail_items", "");
                }
                else
                {
                    foreach (var detail in details)
                    {
                        sb.Append(string.Format("<div class='detail-title {0}'>{1}</div>", Zebra(i++), detail.title));
                        string[] lines = detail.text.Trim().Split(new char[] { '\n' });
                        foreach (string line in lines)
                        {
                            sb.Append(string.Format("<div class='{0}'>{1}</div>", Zebra(i++), line));
                        }
                    }
                    main_htm = ReplaceGuid(main_htm, "detail_items", sb.ToString());
                }

                sb.Clear();
                Db.doctor[] drs = db.GetDoctors(recentOnly: true, patient: id);
                if (drs == null)
                    throw new Exception("No recent doctors");
                foreach (var d in drs)
                {
                    sb.Append(string.Format("<div class='name {0}'>",Zebra(i++)));
                    if (d.firstname != string.Empty)
                        sb.Append(d.firstname);
                    if (d.lastname != string.Empty)
                        sb.Append(d.lastname);
                    sb.Append("</div>");
                    if (d.telephone != string.Empty)
                    {
                        sb.Append(string.Format("<div class='{0}'>Phone:",Zebra(i++)));
                        sb.Append(d.telephone);
                        sb.Append("</div>");
                    }
                }
                main_htm = ReplaceGuid(main_htm, "doctors", sb.ToString());
                sb.Clear();
                WriteFile(destination + "main.html", main_htm);
                 
            }
	        catch (Exception ex)
	        {
                result["status"] = "error";
                result["reason"] = ex.Message;
	        }
            return result;
        }
        private Stream GetResourceAsStream(string name)
        {
            Stream stream = Assembly.GetEntryAssembly().GetManifestResourceStream("Concierge_Manager.Properties.Resources.resources");
            ResourceReader reader = new ResourceReader(stream);
            string resourceType = "";
            byte[] data = null;
            reader.GetResourceData(name, out resourceType , out data );
            MemoryStream ms = new MemoryStream(data, 4, data.Length - 4); // it seems that we are getting the file length in the first four bytes
            return ms;
        }
        private string GetResource(string name)
        {
            string result = string.Empty;
            Stream stream = Assembly.GetEntryAssembly().GetManifestResourceStream("Concierge_Manager.Properties.Resources.resources");
            IResourceReader reader = new ResourceReader(stream);
            var query = from DictionaryEntry entry in reader
                        where entry.Key as string == name
                        select entry.Value;
            if (query.Count() == 1)
            {
                result = query.First() as string;
            }
            reader.Dispose();
            stream.Dispose();
            return result;
        }
        private void WriteFile(string name, string value)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            FileStream fs = new FileStream(name, FileMode.Create, FileAccess.Write);
            byte[] data = encoding.GetBytes(value);
            fs.Write(data, 0, data.Length);
            fs.Close();
            fs.Dispose();
        }
        public Hashtable CreateWebsite_(XElement root)
        {
            ASCIIEncoding encoding = new ASCIIEncoding();
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            string[] names = Assembly.GetEntryAssembly().GetManifestResourceNames();
            Stream stream = Assembly.GetEntryAssembly().GetManifestResourceStream("Concierge_Manager.Properties.Resources.resources");
            stream.Position = 0;
            //stream.Read(buffer,0,(int)(stream.Length));
            //IResourceReader reader = new ResourceReader("Concierge_Manager.Properties.Resources.resources");
            IResourceReader reader = new ResourceReader(stream);
//            string hash = string.Concat((from b in hashBytes
//                                         select string.Format("{0:X2}", b)).ToArray());
            string rootDir = @"c:\temp\temp";
            Directory.Delete(rootDir, true);
            Directory.CreateDirectory(rootDir);
            Directory.CreateDirectory(rootDir + "\\activities");
            FileStream fs;
            fs = new FileStream(rootDir + @"\index.htm", FileMode.Create, FileAccess.Write);
            string v;
            v = (from DictionaryEntry de in reader
                        where de.Key as string == "index_htm"
                        select de.Value).First() as string;
            fs.Write(encoding.GetBytes(v), 0, v.Length);
            fs.Close();
            fs.Dispose();
            fs = new FileStream(rootDir + @"\css\index.css", FileMode.Create, FileAccess.Write);
            v = (from DictionaryEntry de in reader
                        where de.Key as string == "index_css"
                        select de.Value).First() as string;
            fs.Write(encoding.GetBytes(v), 0, v.Length);
            fs.Close();
            fs.Dispose();
            fs = new FileStream(rootDir + @"\main.htm", FileMode.Create, FileAccess.Write);
            v = (from DictionaryEntry de in reader
                 where de.Key as string == "main_htm"
                 select de.Value).First() as string;
            v = ReplaceGuid(v, "name", "FROST, JACK");
            v = ReplaceGuid(v, "dob", "December 6, 1969");
            v = ReplaceGuid(v, "gender", "Male");
            v = ReplaceGuid(v, "emergencycontact", "nobody @ 212-555-1212");
            v = ReplaceGuid(v, "alerts", "no alerts/healthy");
            Db.Db db = Db.Db.Instance();
            Hashtable activitiesResult = db.GetActivities(1);
            List<Hashtable> items = activitiesResult["items"] as List<Hashtable>;
            if (items.Count == 0)
            {
                v = ReplaceGuid(v, "activities", "None");
            }
            else
            {
                string acc = "";
                int iDocument = 0;
                foreach (Hashtable item in items)
                {
                    string fileName = rootDir+@"\activities\"+iDocument.ToString()+".pdf";
                    FileStream docStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                    byte[] data = item["document"] as byte[];
                    docStream.Write(data, 0, data.Length);
                    docStream.Close();
                    string zebra = (iDocument % 2) == 0 ? "even" : "odd";
                    string row = string.Format(@"<a href='activities/{0}.pdf' target='_blank'><div ><div class='row {5}'><div class='specialty left'>{1}</div><div class='subspecialty'>{2}</div><div class='date'>{3}</div></div></div></a>", 
                        iDocument, item["procedure"], item["specialty"], item["subspecialty"], item["date"],zebra);
                    acc += row;
                    iDocument++;
                }
                v = ReplaceGuid(v, "activities", acc);
            }
            fs.Write(encoding.GetBytes(v), 0, v.Length);
            fs.Close();
            fs.Dispose();
            fs = new FileStream(rootDir + @"\css\main.css", FileMode.Create, FileAccess.Write);
            v = (from DictionaryEntry de in reader
                 where de.Key as string == "main_css"
                 select de.Value).First() as string;
            fs.Write(encoding.GetBytes(v), 0, v.Length);
            fs.Close();
            fs.Dispose();
            return result;
        }
        Hashtable savedPaths = new Hashtable();
        public Hashtable SetCurrentDirectory(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Hashtable fileInfo = new Hashtable();
                result["fileInfo"] = fileInfo;
                XElement pathElement = root.XPathSelectElement("//path");
                if (pathElement != null)
                {
                    string path = pathElement.Value;
                    if (path.Length == 2 && path[1] == ':')
                    {
                        string currentDirectoryPath = Directory.GetCurrentDirectory();
                        if (currentDirectoryPath[1] == ':')
                        {
                            savedPaths[currentDirectoryPath[0]] = currentDirectoryPath;
                        }
                        string saved = savedPaths[path[0]] as string;
                        if (saved == null)
                        {
                            savedPaths[path[0]] = saved = string.Format("{0}:\\", path[0]);
                        }
                        Directory.SetCurrentDirectory(saved);
                    }
                    else
                    {
                        Directory.SetCurrentDirectory(path);
                    }
                }


                List<Hashtable> volumes = new List<Hashtable>();
                fileInfo["volumes"] = volumes;
                foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                {
                    if (!driveInfo.IsReady)
                        continue;
                    Hashtable volume = new Hashtable();
                    volume["Name"] = driveInfo.Name.Substring(0, 2);
                    volumes.Add(volume);
                }
                string currentDirectory = Directory.GetCurrentDirectory();
                fileInfo["currentPath"] = currentDirectory;
                DirectoryInfo directoryInfoCurrent = new DirectoryInfo(currentDirectory);
                List<Hashtable> folders = new List<Hashtable>();
                fileInfo["folders"] = folders;
                DirectoryInfo directoryInfoParent = directoryInfoCurrent.Parent;
                if (directoryInfoParent != null)
                {
                    Hashtable folder = new Hashtable();
                    folders.Add(folder);
                    folder["Name"] = "..";
                    folder["FullName"] = directoryInfoParent.FullName;
                }
                foreach (DirectoryInfo directoryInfo in directoryInfoCurrent.GetDirectories())
                {
                    if ((directoryInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;
                    Hashtable folder = new Hashtable();
                    folders.Add(folder);
                    folder["Name"] = directoryInfo.Name;
                    folder["FullName"] = directoryInfo.FullName;
                }
                List<Hashtable> files = new List<Hashtable>();
                fileInfo["files"] = files;
                foreach (FileInfo fileInfo2 in directoryInfoCurrent.GetFiles())
                {
                    Hashtable file = new Hashtable();
                    files.Add(file);
                    file["Name"] = fileInfo2.Name;
                    file["FullName"] = fileInfo2.FullName;
                    file["Length"] = fileInfo2.Length;
                    file["Extension"] = fileInfo2.Extension;
                    file["LastWriteTime"] = fileInfo2.LastWriteTime.ToString();
                    file["CreationTime"] = fileInfo2.CreationTime.ToString();
                }
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["info"] = "SetCurrentDirectory exception";
            }
            return result;
        }
        public Hashtable GetSpecialFile(XElement root)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Hashtable values = new Hashtable();
                string name = root.XPathSelectElement("//name").Value;
                values["name"] = name;
                Db.Db db = Db.Db.Instance();
                return db.GetSpecialFile(values);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = ex.Message;
            }
            return result;
        }
        public Hashtable AddFile(XElement root)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                Hashtable values = new Hashtable();
                string path = root.XPathSelectElement("//path").Value;
                string name = root.XPathSelectElement("//name").Value;
                values["path"] = path;
                values["name"] = name;
                Db.Db db = Db.Db.Instance();
                return db.AddFile(values);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = ex.Message;
            }
            return result;
        }
        public Hashtable DeleteDocument(XElement root)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string id = root.XPathSelectElement("//id").Value;
                Db.Db db = Db.Db.Instance();
                db.DeleteDocument(int.Parse(id));
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = ex.Message;
            }
            return result;
        }
        /*
        public Hashtable AddSpecialty(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string specialty = root.XPathSelectElement("//specialty").Value;
                string subspecialty = root.XPathSelectElement("//subspecialty").Value;
                Db.Db db = Db.Db.Instance();
                db.AddSpecialty(specialty, subspecialty);
            }
            catch (Exception)
            {
                result["status"] = "error";
                result["reason"] = "AddSpecialty failed";
            }
            return result;
        }
         */
        public Hashtable UpdateDoctor(XElement root)
        {
            var result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string firstName = string.Empty, lastName = string.Empty, shortName = string.Empty, address1 = string.Empty, address2 = string.Empty;
                string address3 = string.Empty, city = string.Empty, locality1 = string.Empty, locality2 = string.Empty, postalCode = string.Empty;
                string country = string.Empty, telephone = string.Empty, fax = string.Empty, email = string.Empty, contact = string.Empty;
                firstName = root.XPathSelectElement("//firstname").Value.Trim();
                lastName = root.XPathSelectElement("//lastname").Value.Trim();
                shortName = root.XPathSelectElement("//shortname").Value.Trim();
                address1 = root.XPathSelectElement("//address1").Value.Trim();
                address2 = root.XPathSelectElement("//address2").Value.Trim();
                address3 = root.XPathSelectElement("//address3").Value.Trim();
                city = root.XPathSelectElement("//city").Value.Trim();
                locality1 = root.XPathSelectElement("//locality1").Value.Trim();
                locality2 = root.XPathSelectElement("//locality2").Value.Trim();
                postalCode = root.XPathSelectElement("//postalcode").Value.Trim();
                country = root.XPathSelectElement("//country").Value.Trim();
                telephone = root.XPathSelectElement("//telephone").Value.Trim();
                fax = root.XPathSelectElement("//fax").Value.Trim();
                email = root.XPathSelectElement("//email").Value.Trim();
                contact = root.XPathSelectElement("//contactperson").Value.Trim();
                Db.Db db = Db.Db.Instance();
                db.UpdateDoctor(firstName, lastName, shortName, address1, address2, address3, city, locality1, locality2, postalCode, country, telephone, fax, email, contact);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "Database error:" + ex.Message;
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
                firstName = root.XPathSelectElement("//firstname").Value.Trim();
                lastName = root.XPathSelectElement("//lastname").Value.Trim();
                shortName = root.XPathSelectElement("//shortname").Value.Trim();
                address1 = root.XPathSelectElement("//address1").Value.Trim();
                address2 = root.XPathSelectElement("//address2").Value.Trim();
                address3 = root.XPathSelectElement("//address3").Value.Trim();
                city = root.XPathSelectElement("//city").Value.Trim();
                locality1 = root.XPathSelectElement("//locality1").Value.Trim();
                locality2 = root.XPathSelectElement("//locality2").Value.Trim();
                postalCode = root.XPathSelectElement("//postalcode").Value.Trim();
                country = root.XPathSelectElement("//country").Value.Trim();
                voice = root.XPathSelectElement("//voice").Value.Trim();
                voice = root.XPathSelectElement("//fax").Value.Trim();
                email = root.XPathSelectElement("//email").Value.Trim();
                contact = root.XPathSelectElement("//contact").Value.Trim();
                Db.Db db = Db.Db.Instance();
                db.AddDoctor(firstName, lastName, shortName, address1, address2, address3, city, locality1, locality2, postalCode, country, voice, fax, email, contact);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = "Database error:"+ex.Message;
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
