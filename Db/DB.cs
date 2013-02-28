using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Collections;
using System.Text.RegularExpressions;

namespace Db
{
    public class Db
    {
        static private Db db = null;
        conciergeEntities _conciergeEntities_;
        const int MaxDocumentSegmentSize = 0x4000;

        #region private
        // private constructor, we're a singleton
        private Db()
        {
            //conciergeEntities_ = new conciergeEntities();
            //conciergeEntities_.Connection.Open();
        }
        // TODO: refactor, this is a copy/paste from ObjectForScripting
        private string Hash(FileInfo fileInfo)
        {
            SHA1Managed sha1 = new SHA1Managed();
            FileStream inFile = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            byte[] hashBytes = sha1.ComputeHash(inFile);
            string hash = string.Concat((from b in hashBytes
                                         select string.Format("{0:X2}", b)).ToArray());
            return hash;
        }

        // called by AddFiles(FileInfo[] files),AddActivities(activity[] activities, FileInfo[] files)
        private int AddFile_(FileInfo file,conciergeEntities conciergeEntities)
        {
            int id;
            string hash = FileHash(file);

            document doc = conciergeEntities.document.CreateObject();
            doc.path = file.FullName;
            doc.checksum = Hash(file);
            conciergeEntities.document.AddObject(doc);
            conciergeEntities.SaveChanges();
            id = (from d in conciergeEntities.document
                  select d.id).Max();
            doc = (from d in conciergeEntities.document
                   where d.id == id
                   select d).First();
            FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            SHA1Managed sha1 = new SHA1Managed();
            int fullSegments = (int)(fileStream.Length / MaxDocumentSegmentSize);
            int leftOver = (int)(fileStream.Length % MaxDocumentSegmentSize);
            for (int iSegment = 0; iSegment < fullSegments; iSegment++)
            {
                byte[] buffer = new byte[MaxDocumentSegmentSize];
                int position = iSegment * MaxDocumentSegmentSize;
                fileStream.Read(buffer, 0, MaxDocumentSegmentSize);
                sha1.TransformBlock(buffer, 0, MaxDocumentSegmentSize, null, 0);
                document_segment segment = conciergeEntities.document_segment.CreateObject();
                segment.document_id = id;
                segment.data = buffer;
                segment.position = iSegment;
                conciergeEntities.document_segment.AddObject(segment);
            }
            if (leftOver > 0)
            {
                byte[] buffer = new byte[leftOver];
                int position = (int)(fileStream.Length - leftOver);
                fileStream.Read(buffer, 0, leftOver);
                sha1.TransformFinalBlock(buffer, 0, leftOver);
                document_segment segment = conciergeEntities.document_segment.CreateObject();
                segment.document_id = id;
                segment.data = buffer;
                segment.position = fullSegments;
                conciergeEntities.document_segment.AddObject(segment);
            }
            else
                sha1.TransformFinalBlock(null, 0, 0);
            string hashKey = string.Concat(from b in sha1.Hash
                                           select b.ToString("X2"));
            doc.checksum = hashKey;
            doc.id = id;
            conciergeEntities.SaveChanges();

            return id;
        }
        public Hashtable[] Specialties()
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                Hashtable[] result = new Hashtable[(from p in conciergeEntities.specialty
                                                    select p).Count()];
                int i = 0;
                foreach (var s in (from s in conciergeEntities.specialty select s))
                {
                    result[i] = new Hashtable();
                    result[i]["specialty"] = s.specialty_name;
                    result[i]["subspecialty"] = s.subspecialty_name;
                    i++;
                }
                conciergeEntities.SaveChanges();
                return result;
            }
        }
        public Hashtable[] Patients()
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                Hashtable[] result = new Hashtable[(from p in conciergeEntities.patient
                                                    select p).Count()];
                int i = 0;
                foreach (var p in (from p in conciergeEntities.patient select p))
                {
                    result[i] = new Hashtable();
                    result[i]["first"] = p.first;
                    result[i]["last"] = p.last;
                    result[i]["dob"] = p.dob;
                    result[i]["gender"] = p.gender;
                    result[i]["emercency_contact"] = p.emergency_contact;
                    i++;
                }
                conciergeEntities.SaveChanges();
                return result;
            }
        }
        // called by AddFile(FileInfo file)
        private int AddFiles(FileInfo[] files)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                {
                    foreach (FileInfo file in files)
                    {
                        int result = AddFile_(file,conciergeEntities);
                    }
                    transaction.Commit();
                }
                return 0;
            }
        }
        // called by AddFile_
        private string FileHash(FileInfo file)
        {
            FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            SHA1Managed sha1 = new SHA1Managed();
            return string.Concat(from b in sha1.ComputeHash(stream)
                                 select b.ToString("X2"));
        }
        #endregion 
        // called by AddPatient(XElement)
        public int AddPatient(string firstName, string lastName,string dateOfBirth,string gender,string emergencyContact)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                if ((from patient in conciergeEntities.patient
                     where patient.first == firstName && patient.last == lastName
                     select patient).Count() > 0)
                {
                    return -1; // TODO: handle patient already added.
                }
                patient p = conciergeEntities.patient.CreateObject();
                p.first = firstName;
                p.last = lastName;
                p.dob = DateTime.Parse(dateOfBirth);
                p.gender = gender;
                p.emergency_contact = emergencyContact;
                conciergeEntities.patient.AddObject(p);
                conciergeEntities.SaveChanges();
                return 0;
            }
        }
        public int AddSpecialty(string specialty, string subspecialty)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                specialty s = conciergeEntities.specialty.CreateObject();
                s.specialty_name = specialty;
                s.subspecialty_name = subspecialty;
                conciergeEntities.specialty.AddObject(s);
                conciergeEntities.SaveChanges();
                return 0;
            }
        }
        // called by AddDoctor(XElement)
        public int AddDoctor(string firstName,string lastName,string shortName,string address1,string address2,string address3,string city,string locality1,string locality2,
            string postalCode,string country,string voice,string fax,string email,string contact)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                shortName = shortName.Trim().ToUpper();
                if ((from dr0 in conciergeEntities.doctor
                     where dr0.shortname == shortName
                     select dr0).Count() > 0)
                {
                    return -1; // TODO: handle short name already extant
                }
                doctor dr = conciergeEntities.doctor.CreateObject();
                dr.firstname = firstName;
                dr.lastname = lastName;
                dr.shortname = shortName;
                dr.address1 = address1;
                dr.address2 = address2;
                dr.address3 = address3;
                dr.city = city;
                dr.locality1 = locality1;
                dr.locality2 = locality2;
                dr.postal_code = postalCode;
                dr.country = country;
                dr.telephone = voice;
                dr.fax = fax;
                dr.email = email;
                dr.contact_person = contact;
                conciergeEntities.doctor.AddObject(dr);
                conciergeEntities.SaveChanges();
                return 0;
            }
        }
        public Hashtable AddActivities(List<Hashtable> items)
        {
            Hashtable result = new Hashtable();
            string fileName = string.Empty;
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                {
                    foreach (var item in items)
                    {
                        string specialty = item["specialty"] as string;
                        string subspecialty = item["subspecialty"] as string;
                        var specialtyQuery = (from s in conciergeEntities.specialty
                                              where s.specialty_name == specialty && s.subspecialty_name == subspecialty
                                              select s.id);
                        if (specialtyQuery.Count() != 1)
                        {
                            result["status"] = "error";
                            result["info"] = "Unknown specialty/subspecialty combination";
                            //                        transaction.Rollback();
                            return result;
                        }
                        int specialty_id = (from s in conciergeEntities.specialty
                                            where s.specialty_name == specialty && s.subspecialty_name == subspecialty
                                            select s.id).First();
                        string firstName = item["firstName"] as string;
                        string lastName = item["lastName"] as string;
                        var patientQuery = (from s in conciergeEntities.patient
                                            where s.first == firstName && s.last == lastName
                                            select s.id);
                        if (patientQuery.Count() != 1)
                        {
                            result["status"] = "error";
                            result["info"] = "Unknown patient";
                            //                        transaction.Rollback();
                            return result;
                        }
                        int patient_id = patientQuery.First();
                        string fullFileName = item["path"] as string;
                        FileInfo fileInfo = new FileInfo(fullFileName);
                        fileName = fileInfo.Name;
                        //  (fileInfo.Name
                        Match m = Regex.Match(fileName, @"^.*\-\-DOCTOR\-\-(?<name>[^\-]*)\-\-.*$");
                        string doctorName = m.Groups["name"].Value;
                        var doctorQuery = (from s in conciergeEntities.doctor
                                           where s.shortname == doctorName
                                           select s.id);
                        if (doctorQuery.Count() != 1)
                        {
                            result["status"] = "error";
                            result["info"] = "Unknown doctor";
                            return result;
                        }
                        int doctor_id = doctorQuery.First();
                        m = Regex.Match(fileName, @"^(?<month>\d{2})(?<day>\d{2})(?<year>\d{4}).*$");
                        DateTime dt = new DateTime(int.Parse(m.Groups["year"].Value), int.Parse(m.Groups["month"].Value), int.Parse(m.Groups["day"].Value));
                        m = Regex.Match(fileName, @"^.*\-\-PROCEDURE\-\-(?<name>[^\-]*)\-\-.*$");
                        string procedureName = m.Groups["name"].Value;
                        m = Regex.Match(fileName, @"^.*\-\-LOCATION\-\-(?<name>[^\-]*)\-\-.*$");
                        string location = m.Groups["name"].Value;
                        int file_id = AddFile_(fileInfo, conciergeEntities);
                        conciergeEntities.SaveChanges();

                        activity activity2 = conciergeEntities.activity.CreateObject();
                        activity2.specialty_id = specialty_id;
                        activity2.doctor_id = doctor_id;
                        activity2.date = dt;
                        activity2.patient_id = patient_id;
                        activity2.procedure = procedureName;
                        activity2.location = location;
                        activity2.document_id = file_id;
                        //activity activity2 = activity.Createactivity(specialty_id,doctor_id,dt,patient_id,procedureName,location,file_id);
                        var foo = (from d in conciergeEntities.doctor
                                   where d.id == doctor_id
                                   select d).First();
                        //activity2.doctorReference.Attach(foo);
                        //activity2.doctorReference.Load();
                        conciergeEntities.activity.AddObject(activity2);
                        conciergeEntities.SaveChanges();

                        try
                        {
                            conciergeEntities.SaveChanges();
                            conciergeEntities.Refresh(System.Data.Objects.RefreshMode.StoreWins, conciergeEntities.activity);
                        }
                        catch (Exception ex)
                        {

                            throw;
                        }
                    }
                    //                transaction.Commit();
                }
            }
            return result;
        }
        // called by AddActivities(XElement root)
        public Hashtable AddActivities(activity[] activities, FileInfo[] files)
        {
            var result = new Hashtable();
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                {
                    int count = activities.Length;
                    for (int i = 0; i < activities.Length; i++)
                    {
                        int fileId;
                        try
                        {
                            fileId = AddFile_(files[i], conciergeEntities);
                        }
                        catch (Exception)
                        {
                            result["status"] = "error";
                            result["reason"] = "Could not open file '" + files[i].FullName + "'";
                            return result;
                        }
                        try
                        {
                            activity activity = conciergeEntities.activity.CreateObject();
                            activity.date = activities[i].date;
                            activity.doctor_id = activities[i].doctor_id;
                            activity.document_id = fileId;
                            activity.location = activities[i].location;
                            activity.patient_id = activities[i].patient_id;
                            activity.procedure = activities[i].procedure;
                            activity.specialty_id = activities[i].specialty_id;
                            conciergeEntities.SaveChanges();
                        }
                        catch (Exception)
                        {
                            result["status"] = "error";
                            result["reason"] = "Could not activity";
                            return result;
                        }
                    }
                    try
                    {
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        result["status"] = "error";
                        result["reason"] = "Could not commit activities";
                        return result;
                    }
                    result["status"] = "ok";
                }
            }
            return result;
        }
        // called by UploadFile(XElement root)
        public int AddFile(FileInfo file)
        {
            FileInfo[] files = new FileInfo[1];
            files[0] = file;
            return AddFiles(files);
        }
        static public Db Instance()
        {
            if (db == null)
            {
                db = new Db();
            }
            return db;
        }
    }
}
