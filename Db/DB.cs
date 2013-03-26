using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Collections;
using System.Text.RegularExpressions;
using System.Security.Principal;

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

            System.Data.Objects.ObjectParameter op = new System.Data.Objects.ObjectParameter("CurrentDate",typeof(DateTime));
            conciergeEntities.GetServerDate(op);
            DateTime dt = (DateTime)op.Value;
            document doc = conciergeEntities.document.CreateObject();
            doc.path = file.FullName;
            doc.checksum = Hash(file);
            doc.added_date = dt;
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
        public byte[] GetDocumentData(int document_id)
        {
            byte[] result;
            using (conciergeEntities conciergeEntities = new conciergeEntities())
            {
                int block = 0;
                var query2 = (from seg in conciergeEntities.document_segment
                              where seg.document_id == document_id
                              orderby seg.position
                              select seg.data);
                byte[] acc = null;
                foreach (byte[] buf in query2)
                {
                    if (acc == null)
                    {
                        acc = buf.Clone() as byte[];
                    }
                    else
                    {
                        byte[] temp = new byte[acc.Length + buf.Length];
                        acc.CopyTo(temp, 0);
                        buf.CopyTo(temp, acc.Length);
                        acc = temp;
                    }
                    block++;
                }
                result = acc;
            }
            return result;
        }
        doctor GetDoctor(int doctor_id)
        {
            doctor result;
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                var query = (from d in conciergeEntities.doctor
                             where d.id == doctor_id
                             select d);
                result = query.First();
            }
            return result;
        }
        public detail_item[] GetDetails(int patient)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                IQueryable<detail_item> query;
                query = from detail in conciergeEntities.detail_item
                        where detail.patient_id == patient
                        select detail;
                if (query.Count() == 0)
                    return null;
                return query.ToArray();
            }
        }
        public doctor[] GetDoctors(bool recentOnly,int patient)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                IQueryable<doctor> query;
                if (recentOnly)
                {
                    query = from doct in conciergeEntities.doctor
                            join dr_pat in conciergeEntities.doctor_patient on doct.id equals dr_pat.doctor_id
                            where dr_pat.recent == true && dr_pat.released == true && dr_pat.patient_id == patient
                            select doct;
                }
                else
                {
                    query = from doct in conciergeEntities.doctor
                            join dr_pat in conciergeEntities.doctor_patient on doct.id equals dr_pat.doctor_id
                            where dr_pat.released == true && dr_pat.patient_id == patient
                            select doct;
                }
                if (query.Count() == 0)
                    return null;
                doctor[] doctors = new doctor[query.Count()];
                int iDoctor = 0;
                foreach (doctor d in query)
                {
                    doctors[iDoctor] = d;
                    iDoctor++;
                }
                return doctors;
            }
        }
        public doctor[] GetDoctors()
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                var query = (from d in conciergeEntities.doctor
                             select d);
                return query.ToArray();
            }
        }
        //        public int AddDoctor(string firstName,string lastName,string shortName,string address1,string address2,string address3,string city,string locality1,string locality2,
//            string postalCode,string country,string voice,string fax,string email,string contact)
        public void UpdateDoctor(string firstName, string lastName, string shortName, string address1, string address2, string address3, string city, string locality1, string locality2,
            string postalCode, string country, string voice, string fax, string email, string contact)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                doctor dr = (from d in conciergeEntities.doctor
                             where shortName == d.shortname
                             select d).First();
                dr.address1 = address1;
                dr.address2 = address2;
                dr.address3 = address3;
                dr.city = city;
                dr.contact_person = contact;
                dr.country = country;
                dr.email = email;
                dr.fax = fax;
                dr.firstname = firstName;
                dr.lastname = lastName;
                dr.locality1 = locality1;
                dr.locality2 = locality2;
                dr.postal_code = postalCode;
                dr.telephone = voice;
                conciergeEntities.SaveChanges();
            }

        }
        string[] GetSpecialty(int specialty_id)
        {
            string[] result = new string[2];
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                var query = (from s in conciergeEntities.specialty
                             where s.id == specialty_id
                             select s);
                result[0] = query.First().specialty_name;
                result[1] = query.First().subspecialty_name;
            }
            return result;
        }/*
        public Hashtable GetActivitiesByDoctor(bool recentOnly, int patient)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            List<Hashtable> drs = new List<Hashtable>();
            try 
	        {	        
                using (conciergeEntities conciergeEntities = new conciergeEntities())
                {
                    int[] dr_ids;
                    if (recentOnly)
                    {
                        dr_ids =
                            (from dr_pat in conciergeEntities.doctor_patient
                             join dr in conciergeEntities.doctor on dr_pat.doctor_id equals dr.id
                             where dr_pat.recent && dr_pat.patient_id == patient && dr_pat.released
                             orderby dr.lastname ascending, dr.firstname ascending
                             select dr_pat.doctor_id).Distinct().ToArray();
                    }
                    else
                    {
                        dr_ids =
                            (from dr_pat in conciergeEntities.doctor_patient
                             join dr in conciergeEntities.doctor on dr_pat.doctor_id equals dr.id
                             where dr_pat.patient_id == patient && dr_pat.released
                             orderby dr.lastname ascending, dr.firstname ascending
                             select dr_pat.doctor_id).Distinct().ToArray();
                    }
                    foreach (int dr_id in dr_ids)
                    {
                        Hashtable current = new Hashtable();
                        drs.Add(current);

                    }
                }
	        }
	        catch (Exception ex)
	        {
		
	        }
        }*/
        public Hashtable GetActivities(bool recent, int patient, int doctor)
        {
            conciergeEntities conciergeEntities;
            List<Hashtable> acts = new List<Hashtable>();
            Hashtable result = new Hashtable();
            using (conciergeEntities = new conciergeEntities())
            {
                var query1 =
                    from a in conciergeEntities.activity
                    join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                    join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                    where dp.patient_id == patient && dp.doctor_id == doctor && dp.recent && dp.released
                    select new { a = a, specialty_id = ds.specialty_id };
                var query2 =
                    from a in conciergeEntities.activity
                    join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                    join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                    where dp.patient_id == patient && dp.doctor_id == doctor && dp.released
                    select new { a = a, specialty_id = ds.specialty_id };
                foreach (var r in recent ? query1 : query2)
                {
                    Hashtable current = new Hashtable();
                    current["procedure"] = r.a.procedure;
                    current["specialtyid"] = r.specialty_id;
                    current["documentid"] = r.a.document_id;
                    current["location"] = r.a.location;
                    current["date"] = r.a.date.ToShortDateString();
                    current["binarydate"] = r.a.date;
                    acts.Add(current);
                }
                result["items"] = acts;
                conciergeEntities.Connection.Open();
            }
            foreach (Hashtable act in acts)
            {
                act["document"] = GetDocumentData((int)(act["documentid"]));
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
            }
            return result;
        }
        public Hashtable GetActivities(bool recent,int patient)
        {
            conciergeEntities conciergeEntities;
            List<Hashtable> acts = new List<Hashtable>();
            Hashtable result = new Hashtable();
            using (conciergeEntities = new conciergeEntities())
            {
                var query01 = from a in conciergeEntities.activity
                              join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                              join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                              where dp.patient_id == patient && dp.released && dp.recent
                              select new { a = a, ds = ds };
                var query02 = from a in conciergeEntities.activity
                              join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                              join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                              where dp.patient_id == patient && dp.released && dp.released
                              select new { a = a, ds = ds };
                foreach (var r in recent ? query01 : query02)
                {
                    Hashtable current = new Hashtable();
                    current["procedure"] = r.a.procedure;
                    current["specialtyid"] = r.ds.specialty_id;
                    current["doctorid"] = r.ds.doctor_id;
                    current["documentid"] = r.a.document_id;
                    current["location"] = r.a.location;
                    current["date"] = r.a.date.ToShortDateString();
                    current["binarydate"] = r.a.date;
                    acts.Add(current);
                }
                result["items"] = acts;
                conciergeEntities.Connection.Open();
            }
            foreach (Hashtable act in acts)
            {
                act["document"] = GetDocumentData((int)(act["documentid"]));
                act["doctor"] = GetDoctor((int)(act["doctorid"]));
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
            }
            return result;
        }
        public Hashtable GetActivities(int patient)
        {
            conciergeEntities conciergeEntities;
            List<Hashtable> acts = new List<Hashtable>();
            Hashtable result = new Hashtable();
            using (conciergeEntities = new conciergeEntities())
            {
                // TODO: check to see if we want both released and unrelease doctor_patient items
                var query =
                    from a in conciergeEntities.activity
                    join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                    join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                    where dp.patient_id == patient
                    select new { a = a, ds = ds };
/*                var query = (from a in conciergeEntities.activity
                             where a.patient_id == patient
                             select a);*/

                // [query.Count()];
                int i = 0;
                foreach (var r in query)
                {
                    Hashtable current = new Hashtable();
                    current["procedure"] = r.a.procedure;
                    current["specialtyid"] = r.ds.specialty_id;
                    current["doctorid"] = r.ds.doctor_id;
                    current["documentid"] = r.a.document_id;
                    current["location"] = r.a.location;
                    current["date"] = r.a.date.ToShortDateString();
                    acts.Add(current);
                    i++;
                }
                result["items"] = acts;
                conciergeEntities.Connection.Open();
            }
            foreach (Hashtable act in acts)
            {
                act["document"] = GetDocumentData((int)(act["documentid"]));
                act["doctor"] = GetDoctor((int)(act["doctorid"]));
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
            }
            return result;
        }
        public byte[] DownloadFile(int fileId)
        {
            conciergeEntities conciergeEntities;
            byte[] result;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                MemoryStream ms = new MemoryStream();
                var query = (from segm in conciergeEntities.document_segment
                           where segm.document_id == fileId
                           orderby segm.position ascending
                           select segm.data);
                foreach(var item in query)
                {
                    ms.Write(item, 0, item.Length);
                }
                result = ms.ToArray();
            }
            return result;
        }
        public Hashtable[] Doctors()
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                Hashtable[] result = new Hashtable[(from d in conciergeEntities.doctor
                                                    select d).Count()];
                int i = 0;
                foreach (var d in (from d in conciergeEntities.doctor select d))
                {
                    result[i] = new Hashtable();
                    result[i]["address1"] = d.address1;
                    result[i]["address2"] = d.address2;
                    result[i]["address3"] = d.address3;
                    result[i]["city"] = d.city;
                    result[i]["contact_person"] = d.contact_person;
                    result[i]["country"] = d.country;
                    result[i]["email"] = d.email;
                    result[i]["fax"] = d.fax;
                    result[i]["firstname"] = d.firstname;
                    result[i]["id"] = d.id;
                    result[i]["lastname"] = d.lastname;
                    result[i]["locality1"] = d.locality1;
                    result[i]["locality2"] = d.locality2;
                    result[i]["postal_code"] = d.postal_code;
                    result[i]["shortname"] = d.shortname;
                    result[i]["telephone"] = d.telephone;
                    i++;
                }
                conciergeEntities.SaveChanges();
                return result;
            }
        }
        public Hashtable[] BrowseDocuments()
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                var query = from docu in conciergeEntities.document
                          join acti in conciergeEntities.activity on docu.id equals acti.document_id
                          join dr_spe in conciergeEntities.doctor_specialty on acti.doctor_specialty_id equals dr_spe.id
                          join doct in conciergeEntities.doctor on dr_spe.doctor_id equals doct.id
                          join spec in conciergeEntities.specialty on dr_spe.specialty_id equals spec.id
                          join dr_pat in conciergeEntities.doctor_patient on acti.doctor_patient_id equals dr_pat.id
                          join pati in conciergeEntities.patient on dr_pat.patient_id equals pati.id
                          select new
                          {
                              Specialty = spec.specialty_name,
                              Subspecialty = spec.subspecialty_name,
                              Doctor = doct.shortname,
                              Procedure = acti.procedure,
                              Location = acti.location,
                              Patient = pati.first + " " + pati.last,
                              Checksum = docu.checksum,
                              Path = docu.path,
                              Id = docu.id
                          };/*
                var foo =
                    from docu in conciergeEntities.document
                    join acti in conciergeEntities.activity on docu.id equals acti.document_id
                    //join doct in conciergeEntities.doctor on acti.doctor_id equals doct.id
                    join dr_spe in conciergeEntities.doctor_specialty on acti.doctor_specialty_id equals dr_spe.id
                    //join spec in conciergeEntities.specialty on acti.specialty_id equals spec.id
                    join dr_pat in conciergeEntities.doctor_patient on acti.doctor_patient_id equals dr_pat.id
                    join pait in conciergeEntities.patient on acti.patient_id equals pait.id
                    select new
                    {
                        Specialty = dr_spe.specialty_name,
                        Subspecialty = spec.subspecialty_name,
                        Doctor = doct.shortname,
                        Procedure = acti.procedure,
                        Location = acti.location,
                        Patient = pait.first + " " + pait.last,
                        Checksum = docu.checksum,
                        Path = docu.path,
                        Id = docu.id
                    };*/
                Hashtable[] result = new Hashtable[query.Count()];
                int i = 0;
                foreach (var item in query)
                {
                    result[i] = new Hashtable();
                    result[i]["Specialty"] = item.Specialty;
                    result[i]["Subspecialty"] = item.Subspecialty;
                    result[i]["Doctor"] = item.Doctor;
                    result[i]["Procedure"] = item.Procedure;
                    result[i]["Location"] = item.Location;
                    result[i]["Patient"] = item.Patient;
                    result[i]["Checksum"] = item.Checksum;
                    result[i]["Path"] = item.Path;
                    result[i]["Id"] = item.Id.ToString();
                    i++;
                }
                return result;
            }
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
        public Hashtable GetPatient(int id)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities conciergeEntities = new conciergeEntities())
                {
                    var query = from patient in conciergeEntities.patient
                                where patient.id == id
                                select patient;
                    if (query.Count() == 0)
                        throw new Exception("Patient not found");
                    Hashtable data = new Hashtable();
                    result["data"] = data;
                    patient p = query.First();
                    data["dob"] = p.dob;
                    data["emergency_contact"] = p.emergency_contact;
                    data["first"] = p.first;
                    data["gender"] = p.gender;
                    data["last"] = p.last;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = ex.Message;
            }
            return result;
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
        public void AddPatient(string firstName, string lastName,string dateOfBirth,string gender,string emergencyContact)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                if ((from patient in conciergeEntities.patient
                     where patient.first == firstName && patient.last == lastName
                     select patient).Count() > 0)
                {
                    throw new Exception("Patient already exists");
                }
                patient p = conciergeEntities.patient.CreateObject();
                p.first = firstName;
                p.last = lastName;
                p.dob = DateTime.Parse(dateOfBirth);
                p.gender = gender;
                p.emergency_contact = emergencyContact;
                conciergeEntities.patient.AddObject(p);
                conciergeEntities.SaveChanges();
            }
        }
        public void DeleteDocument(int id)
        {
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                {
                    if ((from docu in conciergeEntities.document
                         where docu.id == id
                         select docu).Count() < 1)
                    {
                        throw new Exception("Document not found");
                    }
                    var query = from docu in conciergeEntities.document
                                join segm in conciergeEntities.document_segment on docu.id equals segm.document_id
                                where docu.id == id
                                select segm;
                    foreach (document_segment segm in query)
                    {
                        conciergeEntities.document_segment.DeleteObject(segm);
                    }
                    conciergeEntities.SaveChanges();
                    var query2 = from acti in conciergeEntities.activity
                                 where acti.document_id == id
                                 select acti;
                    foreach (activity acti in query2)
                    {
                        conciergeEntities.activity.DeleteObject(acti);
                    }
                    conciergeEntities.SaveChanges();
                    document doc = (from docu in conciergeEntities.document
                                    where docu.id == id
                                    select docu).First();
                    conciergeEntities.document.DeleteObject(doc);
                    conciergeEntities.SaveChanges();
                    transaction.Commit();
                }
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
            List<string> missings = new List<string>();
            if (firstName == string.Empty)
                missings.Add("First Name");
            if (lastName == string.Empty)
                missings.Add("Last Name");
            if (shortName == string.Empty)
                missings.Add("Short Name");
            if (missings.Count > 0)
            {
                string acc = "Missing fields:";
                foreach(string missing in missings)
                {
                    acc += missing + " ";
                }
                throw new Exception(acc);
            }
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                shortName = shortName.Trim().ToUpper();
                if ((from dr0 in conciergeEntities.doctor
                     where dr0.shortname == shortName
                     select dr0).Count() > 0)
                {
                    throw new Exception("Short name already exists");
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
            result["status"] = "ok";
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
                            result["reason"] = "Unknown specialty/subspecialty combination";
                            //                        transaction.Rollback();
                            transaction.Rollback();
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
                            result["reason"] = "Unknown patient";
                            transaction.Rollback();
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
                            result["reason"] = "Unknown doctor";
                            transaction.Rollback();
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

                        var dpQuery = from dp in conciergeEntities.doctor_patient
                                      where dp.doctor_id == doctor_id && dp.patient_id == patient_id
                                      select dp;
                        doctor_patient dr_pat;
                        switch (dpQuery.Count())
                        {
                            case 0:
                                {
                                    dr_pat = conciergeEntities.CreateObject<doctor_patient>();
                                    dr_pat.patient_id = patient_id;
                                    dr_pat.recent = true; // TODO: do we want to make this a choice, or just default like this
                                    dr_pat.doctor_id = doctor_id;
                                    dr_pat.released = false;
                                    conciergeEntities.doctor_patient.AddObject(dr_pat);
                                    conciergeEntities.SaveChanges();
                                    break;
                                }
                            case 1:
                                {
                                    dr_pat = dpQuery.First();
                                    break;
                                }
                            default:
                                {
                                    throw new Exception("More than one doctor patient relationship exists in the database");
                                }
                        }
                        var dsQuery = from ds in conciergeEntities.doctor_specialty
                                      where ds.doctor_id == doctor_id && ds.specialty_id == specialty_id
                                      select ds;
                        doctor_specialty dr_spe;
                        switch (dsQuery.Count())
                        {
                            case 0:
                                {
                                    dr_spe = conciergeEntities.CreateObject<doctor_specialty>();
                                    dr_spe.specialty_id = specialty_id;
                                    dr_spe.doctor_id = doctor_id;
                                    conciergeEntities.doctor_specialty.AddObject(dr_spe);
                                    conciergeEntities.SaveChanges();
                                    break;
                                }
                            case 1:
                                {
                                    dr_spe = dsQuery.First();
                                    break;
                                }
                            default:
                                {
                                    throw new Exception("More than one doctor specialty relationship exists in the database");
                                }
                        }
                        activity activity2 = conciergeEntities.activity.CreateObject();
                        activity2.doctor_specialty_id = dr_spe.id;
                        activity2.date = dt;
                        activity2.doctor_patient_id = dr_pat.id;
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
                        note note = conciergeEntities.note.CreateObject();
                        note.user = WindowsIdentity.GetCurrent().Name;
                        note.text = "Added file from concierge directory\n";
                        foreach (string key in item.Keys)
                        {
                            note.text += string.Format("{0}: {1}\n", key, item[key]);
                        }
                        note.when = DateTime.Now;
                        conciergeEntities.note.AddObject(note);
                        conciergeEntities.SaveChanges();

                        if ((from doc_pat in conciergeEntities.doctor_patient
                             where doc_pat.doctor_id == doctor_id && doc_pat.patient_id == patient_id
                             select doc_pat).Count() < 1)
                        {
                            doctor_patient doc_pat = conciergeEntities.doctor_patient.CreateObject();
                            doc_pat.doctor_id = doctor_id;
                            doc_pat.patient_id = patient_id;
                            conciergeEntities.doctor_patient.AddObject(doc_pat);
                            conciergeEntities.SaveChanges();
                        }
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
                                    transaction.Commit();
                }
            }
            return result;
        }
        // called by AddActivities(XElement root)
        /*
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
         * */
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
