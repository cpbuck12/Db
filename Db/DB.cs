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
using System.Drawing;
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
        private string BufferToString(byte[] buffer)
        {
            string hash = string.Concat((from b in buffer
                                         select string.Format("{0:X2}",b)).ToArray());
            return hash;
        }/*
        private string Hash(byte[] buffer)
        {
            SHA1Managed sha1 = new SHA1Managed();
            byte[] hashBytes = sha1.ComputeHash(buffer);
            return BufferToString(hashBytes);
        }
        private string Hash(FileStream stream)
        {
            SHA1Managed sha1 = new SHA1Managed();
            byte[] hashBytes = sha1.ComputeHash(stream);
            return BufferToString(hashBytes);
        }
        // TODO: refactor, this is a copy/paste from ObjectForScripting
        private string Hash(FileInfo fileInfo)
        {
            SHA1Managed sha1 = new SHA1Managed();
            FileStream inFile = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            return Hash(inFile);
        }*/
        private string FileHash(byte[] buffer)
        {
            MemoryStream ms = new MemoryStream(buffer,false);
            SHA1Managed sha1 = new SHA1Managed();
            return string.Concat(from b in sha1.ComputeHash(ms)
                                 select b.ToString("X2"));
        }
        private string FileHash(Stream stream)
        {
            SHA1Managed sha1 = new SHA1Managed();
            return string.Concat(from b in sha1.ComputeHash(stream)
                                 select b.ToString("X2"));
        }
        private string FileHash(FileInfo file)
        {
            FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            return FileHash(stream);
        }

        Hashtable RemoveFile_(Hashtable values,conciergeEntities ent)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try 
	        {
                int document_id = (int)result["document_id"];
                var query = from doc in ent.document
                            where doc.id == document_id
                            select doc;
                if(query.Count() < 1)
                    throw new Exception(string.Format("Trying to remove unknown file id {0}",document_id));
                var query2 = from ds in ent.document_segment
                                where ds.document_id == document_id
                                select ds;
                foreach(document_segment ds in query2)
                {
                    ent.document_segment.DeleteObject(ds);
                }
                ent.SaveChanges();
                ent.document.DeleteObject(query.First());
                ent.SaveChanges();
	        }
	        catch (Exception ex)
	        {
                result["status"] = "error";
                result["reason"] = Explain(ex);
	        }
            return result;
        }
        Hashtable GetServerDate(conciergeEntities ent)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try 
	        {	        
                System.Data.Objects.ObjectParameter op = new System.Data.Objects.ObjectParameter("CurrentDate",typeof(DateTime));
                ent.GetServerDate(op);
                DateTime dt = (DateTime)op.Value;
                result["date"] = dt;
	        }
	        catch (Exception ex)
	        {
                result["status"] = "error";
                result["reason"] = Explain(ex);
	        }
            return result;
        }
/*
        private int AddFile_(FileInfo file,conciergeEntities conciergeEntities)
        {
            int id;
            string hash = FileHash(file);

            System.Data.Objects.ObjectParameter op = new System.Data.Objects.ObjectParameter("CurrentDate",typeof(DateTime));
            conciergeEntities.GetServerDate(op);
            DateTime dt = (DateTime)op.Value;
            document doc = conciergeEntities.document.CreateObject();
            doc.path = file.FullName;
            doc.checksum = FileHash(file);
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
        }*/
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
        public doctor[] _GetDoctors()
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
        /*
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
         */
        /*
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
        }
         */
        /*
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
        private string Explain(Exception ex)
        {
            if (ex.InnerException == null)
                return ex.Message;
            else
                return string.Format(ex.Message + " ::::" + ex.InnerException.Message);
        }
        public Hashtable UpdateDoctor(Hashtable values)
        {
            Hashtable result = new Hashtable();
            List<Hashtable> doctorList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                if(values["id"] == null)
                    throw new Exception("Missing doctor id");
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    int id = (int)values["id"];
                    var query = from d in ent.doctor
                                where d.id == id
                                select d;
                    if (query.Count() != 1)
                        throw new Exception("Bad doctor id");
                    doctor doctor = query.First();
                    doctor.address1 = values["address1"] as string;
                    doctor.address2 = values["address2"] as string;
                    doctor.address3 = values["address3"] as string;
                    doctor.city = values["city"] as string;
                    doctor.contact_person = values["contact_person"] as string;
                    doctor.country = values["country"] as string;
                    doctor.email = values["email"] as string;
                    doctor.fax = values["fax"] as string;
                    doctor.firstname = values["firstname"] as string;
                    doctor.lastname = values["lastname"] as string;
                    doctor.locality1 = values["locality1"] as string;
                    doctor.locality2 = values["locality2"] as string;
                    doctor.postal_code = values["postal_code"] as string;
                    doctor.telephone = values["telephone"] as string;
                    ent.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        /*
        public Hashtable GetDoctors()
        {
            Hashtable result = new Hashtable();
            List<Hashtable> doctorList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    foreach (doctor d in (from d in ent.doctor select d))
                    {
                        Hashtable next = new Hashtable();
                        next["address1"] = d.address1;
                        next["address2"] = d.address2;
                        next["address3"] = d.address3;
                        next["city"] = d.city;
                        next["contact_person"] = d.contact_person;
                        next["country"] = d.country;
                        next["email"] = d.email;
                        next["fax"] = d.fax;
                        next["firstname"] = d.firstname;
                        next["id"] = d.id;
                        next["lastname"] = d.lastname;
                        next["locality1"] = d.locality1;
                        next["locality2"] = d.locality2;
                        next["postal_code"] = d.postal_code;
                        next["shortname"] = d.shortname;
                        next["telephone"] = d.telephone;
                        doctorList.Add(next);
                    }
                    result["data"] = doctorList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
         */
        public Hashtable GetPatients()
        {
            Hashtable result = new Hashtable();
            List<Hashtable> patientList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    foreach (patient p in (from p in ent.patient select p))
                    {                  
                        Hashtable next = new Hashtable();
                        next["dob"] = p.dob;
                        next["emergency_contact"] = p.emergency_contact;
                        next["first"] = p.first;
                        next["gender"] = p.gender;
                        next["id"] = p.id;
                        next["last"] = p.last;
                        patientList.Add(next);
                    }
                    result["data"] = patientList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable GetSuffixes()
        {
            Hashtable result = new Hashtable();
            List<Hashtable> suffixList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    foreach (suffix s in (from s in ent.suffix select s))
                    {
                        Hashtable next = new Hashtable();
                        next["value"] = s.value;
                        suffixList.Add(next);
                    }
                    result["data"] = suffixList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable GetProcedures()
        {
            Hashtable result = new Hashtable();
            List<Hashtable> procedureList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    foreach (procedure p in (from p in ent.procedure select p))
                    {
                        Hashtable next = new Hashtable();
                        next["name"] = p.name;
                        procedureList.Add(next);
                    }
                    result["data"] = procedureList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }

        public Hashtable GetTargets(Hashtable values)
        {
            Hashtable result = new Hashtable();
            List<Hashtable> targetList = new List<Hashtable>();
            string procedure_name = (values["procedure_name"] as string).Trim().ToLower();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    List<Hashtable> items = new List<Hashtable>();
                    var query = from pt in ent.procedure_target
                                where pt.procedure_name.Trim().ToLower() == procedure_name
                                select pt.target_name;
                    foreach (string target_name in query)
                    {
                        Hashtable item = new Hashtable();
                        item["target_name"] = target_name;
                        items.Add(item);
                    }
                    result["data"] = items;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        
        public Hashtable GetPatientSpecialties(Hashtable values)
        {
            Hashtable result = new Hashtable();
            List<Hashtable> specialtyList = new List<Hashtable>();
            string procedure_name = (values["procedure_name"] as string).Trim().ToLower();
            int patient_id = (int)values["patient_id"];
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    List<Hashtable> items = new List<Hashtable>();
                    var query = (from ps in ent.procedure_specialty
                                 where ps.procedure_name.Trim().ToLower() == procedure_name
                                 orderby ps.procedure_name
                                 select ps);
                    foreach (procedure_specialty ps in query)
                    {
                        Hashtable h = new Hashtable();
                        h["specialty_name"] = ps.specialty_name;
                        h["procedure_specialty_id"] = ps.id;
                    }
                    foreach (Hashtable item in items)
                    {
                        string specialty_name = item["specialty_name"] as string;
                        int procedure_specialty_id = (int)item["procedure_specialty_id"];
                        int count = (from a in ent.activity
                                      join dp in ent.doctor_patient on a.doctor_patient_id equals dp.id
                                      where a.procedure_specialty_id == procedure_specialty_id && dp.patient_id == patient_id
                                      select a).Count();
                        item["count"] = count;
                    }
                    result["data"] = items;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
         
        
        public Hashtable GetSpecialties(Hashtable values)
        {
            Hashtable result = new Hashtable();
            List<Hashtable> specialtyList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                string procedure_name = (values["procedure_name"] as string).Trim().ToLower();
                using (conciergeEntities ent = new conciergeEntities())
                {
                    var query = (from ps in ent.procedure_specialty
                                 where ps.procedure_name.Trim().ToLower() == procedure_name
                                 orderby ps.specialty_name
                                 select ps);
                    foreach(procedure_specialty ps in query)
                    {
                        Hashtable item = new Hashtable();
                        item["specialty_name"] = ps.specialty_name;
                        item["procedure_specialty_id"] = ps.id;
                        specialtyList.Add(item);
                    }
                    result["data"] = specialtyList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        
        public Hashtable GetLocations()
        {
            Hashtable result = new Hashtable();
            List<Hashtable> locationList = new List<Hashtable>();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    foreach (location l in (from l in ent.location select l))
                    {
                        Hashtable next = new Hashtable();
                        next["name"] = l.name;
                        next["address1"] = l.address1;
                        next["address2"] = l.address2;
                        next["address3"] = l.address3;
                        next["city"] = l.city;
                        next["contact_person"] = l.contact_person;
                        next["country"] = l.country;
                        next["email"] = l.email;
                        next["fax"] = l.fax;
                        next["locality1"] = l.locality1;
                        next["locality2"] = l.locality2;
                        next["postal_code"] = l.postal_code;
                        next["telephone"] = l.telephone;
                        locationList.Add(next);
                    }
                    result["data"] = locationList;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
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
                    select a;
                var query2 =
                    from a in conciergeEntities.activity
                    join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                    join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                    where dp.patient_id == patient && dp.doctor_id == doctor && dp.released
                    select a;
                foreach (var r in recent ? query1 : query2)
                {
                    Hashtable current = new Hashtable();
                    current["documentid"] = r.document_id;
                    current["location_name"] = r.location;
                    current["date"] = r.date.ToShortDateString();
                    current["binarydate"] = r.date;
                    acts.Add(current);
                }
                result["items"] = acts;
                conciergeEntities.Connection.Open();
            }
            foreach (Hashtable act in acts)
            {
                act["document"] = GetDocumentData((int)(act["documentid"]));
                /*
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
                 */
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
                              select a;
                var query02 = from a in conciergeEntities.activity
                              join dp in conciergeEntities.doctor_patient on a.doctor_patient_id equals dp.id
                              join ds in conciergeEntities.doctor_specialty on a.doctor_specialty_id equals ds.id
                              where dp.patient_id == patient && dp.released && dp.released
                              select a;
                foreach (var r in recent ? query01 : query02)
                {
                    Hashtable current = new Hashtable();
                    current["documentid"] = r.document_id;
                    current["location_name"] = r.location;
                    current["date"] = r.date.ToShortDateString();
                    current["binarydate"] = r.date;
                    acts.Add(current);
                }
                result["items"] = acts;
                conciergeEntities.Connection.Open();
            }
            foreach (Hashtable act in acts)
            {
                act["document"] = GetDocumentData((int)(act["documentid"]));
                act["doctor"] = GetDoctor((int)(act["doctorid"]));
                /*
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
                 */
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
                    current["doctorid"] = r.ds.doctor_id;
                    current["documentid"] = r.a.document_id;
                    current["location_name"] = r.a.location;
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
                /*
                string[] sp = GetSpecialty((int)(act["specialtyid"]));
                act["specialty"] = sp[0];
                act["subspecialty"] = sp[1];
                 */
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
        }/*
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
          */
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
                          join dr_pat in conciergeEntities.doctor_patient on acti.doctor_patient_id equals dr_pat.id
                          join pati in conciergeEntities.patient on dr_pat.patient_id equals pati.id
                          select new
                          {
                              Doctor = doct.shortname
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
                        Location = acti.location_name,
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
                    result[i]["Doctor"] = item.Doctor;
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

        /*
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
         */

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
                    //data["signature_document_id"] = p.signature_document_id;
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
        #endregion 
        public Hashtable AddPatient(Hashtable values)
        {
            Hashtable result = new Hashtable();
            try
            {
                string firstName = (values["firstName"] ?? string.Empty) as string;
                string lastName = (values["lastName"] ?? string.Empty) as string;
                string dateOfBirth = (values["dateOfBirth"] ?? string.Empty) as string;
                string gender = (values["gender"] ?? string.Empty) as string;
                string emergencyContact = (values["emergencyContact"] ?? string.Empty) as string;
                AddPatient(firstName, lastName, dateOfBirth, gender, emergencyContact);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            result["status"] = "ok";
            return result;
        }
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
        public Hashtable GetSpecialFile(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string name = ((values["name"] as string) ?? "").Trim();
                if(name == "")
                    throw new Exception("Special file name unspecified");
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    var query1 = from sd in ent.special_document
                                 where sd.name == name
                                 select sd;
                    if (query1.Count() < 1)
                    {
                        throw new Exception("document not found");
                    }
                    int document_id = query1.First().document_id;
                    var query2 = from ds in ent.document_segment
                                 where ds.document_id == document_id
                                 orderby ds.position ascending
                                 select ds;
                    int length = 0;
                    foreach (document_segment ds in query2)
                        length += ds.data.Length;
                    byte[] buffer = new byte[length];
                    MemoryStream ms = new MemoryStream(buffer);
                    foreach (document_segment ds in query2)
                    {
                        ms.Write(ds.data, 0, ds.data.Length);
                    }
                    result["data"] = buffer;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable GetSpecialF_ile(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string name = (values["name"] as string).Trim().ToLower();
                using (conciergeEntities ent = new conciergeEntities())
                {
                    var query = from sd in ent.special_document
                                where sd.name.Trim().ToLower() == name
                                select sd;
                    if (query.Count() == 0)
                    {
                        throw new Exception("unknown special file");
                    }
                    int document_id = query.First().document_id;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable GetFile(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string path = values["path"] as string ?? string.Empty;
                string name = values["name"] as string ?? string.Empty;
                if(values["document_id"] != null && name != string.Empty)
                    throw new Exception("Cannot specify both document_id and name in GetFile");
                if(values["document_id"] == null && name == string.Empty)
                    throw new Exception("Must specify either document_id or name in GetFile");
                int document_id = (int)values["document_id"];
                Stream s;
                MemoryStream ms = null;
                FileStream fs = null;
                if (path != string.Empty)
                {
                    fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
                    s = fs;
                }
                else
                {
                    ms = new MemoryStream();
                    s = ms;
                }
                using (conciergeEntities ent = new conciergeEntities())
                {
                    if(name != "")
                    {
                        name = name.Trim().ToLower();
                        var q2 = from sd in ent.special_document
                            where sd.name.Trim().ToLower() == name
                            select sd;
                        if(q2.Count() == 0)
                            throw new Exception(string.Format("Could not find file named '{0}' in GetFile",name));
                        document_id = q2.First().document_id;
                    }
                    var query = from ds in ent.document_segment
                                where ds.document_id == document_id
                                orderby ds.position
                                select ds;
                    foreach (document_segment ds in query)
                    {
                        s.Write(ds.data, 0, ds.data.Length);
                    }
                }
                if(ms != null)
                {
                    result["data"] = ms.GetBuffer();
                }
                s.Close();
                s.Dispose();
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
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
        /*
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
         */
        /*
        public Hashtable AddDoctor(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string firstName = (values["firstName"] ?? string.Empty) as string;
                string lastName = (values["lastName"] ?? string.Empty) as string;
                string shortName = (values["shortName"] ?? string.Empty) as string;
                string address1 = (values["address1"] ?? string.Empty) as string;
                string address2 = (values["address2"] ?? string.Empty) as string;
                string address3 = (values["address3"] ?? string.Empty) as string;
                string city = (values["city"] ?? string.Empty) as string;
                string locality1 = (values["locality1"] ?? string.Empty) as string;
                string locality2 = (values["locality2"] ?? string.Empty) as string;
                string postalCode = (values["postalCode"] ?? string.Empty) as string;
                string country = (values["country"] ?? string.Empty) as string;
                string voice = (values["voice"] ?? string.Empty) as string;
                string fax = (values["fax"] ?? string.Empty) as string;
                string email = (values["email"] ?? string.Empty) as string;
                string contact = (values["contact"] ?? string.Empty) as string;
                AddDoctor(firstName, lastName, shortName, address1, address2, address3, city, locality1, locality2, postalCode, country, voice, fax, email, contact);
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
         */
        // called by AddDoctor(XElement)
        /*
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
                    acc += string.Format("\"{0}\" ", missing);
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
        */
        public Hashtable GetStamp(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            conciergeEntities conciergeEntities;
            int document_id;
            try
            {
                if (values["patient_id"] == null)
                    throw new Exception("patient_id not specified when calling GetStamp");
                int patient_id = (int)values["patient_id"];
                using (conciergeEntities = new conciergeEntities())
                {
                    conciergeEntities.Connection.Open();
                    var patientQuery = (from pa in conciergeEntities.patient
                                        where pa.id == patient_id
                                        select pa);
                    if (patientQuery.Count() < 1)
                        throw new Exception("Invalid patient_id when calling GetStamp");
                    var signatureQuery = (from ps in conciergeEntities.patient_signature
                                          where ps.patient_id == patient_id
                                          select ps);
                    if(signatureQuery.Count() < 1)
                        throw new Exception("Patient has no signature");
                    document_id = signatureQuery.First().document_id;
                    conciergeEntities.Connection.Close();
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
                return result;
            }
            values["path"] = "";
            values["document_id"] = document_id;
            Hashtable result2 = GetFile(values);
            if (result2["status"] as string != "ok")
                return result2;
            byte[] buffer = result2["data"] as byte[];
            Image image = Image.FromStream(new MemoryStream(buffer));
            try
            {
                result["image"] = image;
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
         
        
        public Hashtable AddStamp(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                if (values["image"] == null)
                    throw new Exception("image not specified when calling AddStamp");
                Image image = values["image"] as Image;
                if (values["patient_id"] == null)
                    throw new Exception("patient_id not specified when calling AddStamp");
                int patient_id = (int)values["patient_id"];
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    using (DbTransaction txn = ent.Connection.BeginTransaction())
                    {
                        //Hashtable values = new Hashtable();
                        MemoryStream ms = new MemoryStream();
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        ms.Position = 0;
                        result = AddFile(ms, ent, "SIGNATURE");
                        if (result["status"] as string != "ok")
                        {
                            txn.Rollback();
                            return result;
                        }
                        ent.SaveChanges();
                        int file_id = (int)result["file_id"];
                        patient_signature ps = ent.patient_signature.CreateObject();
                        ps.document_id = file_id;
                        ps.patient_id = patient_id;
                        ent.patient_signature.AddObject(ps);
                        ent.SaveChanges();
                        txn.Commit();
                    }           
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
         
        /*
        public Hashtable _AddStamp(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            conciergeEntities conciergeEntities;
            try
            {
                if (values["image"] == null)
                    throw new Exception("image not specified when calling AddStamp");
                Image image = values["image"] as Image;
                if (values["patient_id"] == null)
                    throw new Exception("patient_id not specified when calling AddStamp");
                int patient_id = (int)values["patient_id"];
                using (conciergeEntities = new conciergeEntities())
                {
                    conciergeEntities.Connection.Open();
                    System.Data.Objects.ObjectParameter op = new System.Data.Objects.ObjectParameter("CurrentDate", typeof(DateTime));
                    conciergeEntities.GetServerDate(op);
                    DateTime now = (DateTime)op.Value;
                    using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                    {
                        var query = from p in conciergeEntities.patient
                                    where p.id == patient_id
                                    select p;
                        if (query.Count() < 1)
                            throw new Exception("Invalid patient_id when calling AddStamp");
                        patient pat = query.First();
                        int stamp_id;
                        if (pat.signature_document_id != null)
                        {
                            stamp_id = (int)pat.signature_document_id;
                            pat.signature_document_id = null;
                            conciergeEntities.SaveChanges();
                            var querySegments = (from ss in conciergeEntities.stamp_segment
                                                 where ss.id == stamp_id
                                                 select ss);
                            foreach (stamp_segment ss in querySegments)
                            {
                                conciergeEntities.stamp_segment.DeleteObject(ss);
                            }
                            conciergeEntities.SaveChanges();
                            var queryStamp = (from st in conciergeEntities.stamp
                                              where st.id == stamp_id
                                              select st);
                            conciergeEntities.stamp.DeleteObject(queryStamp.First());
                            conciergeEntities.SaveChanges();
                        }
                        MemoryStream ms = new MemoryStream();
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        stamp stamp = conciergeEntities.stamp.CreateObject();
                        stamp.added_date = now;
                        conciergeEntities.stamp.AddObject(stamp);
                        conciergeEntities.SaveChanges();
                        stamp_id = (from st in conciergeEntities.stamp
                                        select st.id).Max();
                        const int MaxSegSize = 0x4000;
                        int fullSegments = (int)(ms.Length / MaxSegSize);
                        int leftOver = (int)(ms.Length % MaxSegSize);
                        ms.Position = 0;
                        for (int iSegment = 0; iSegment < fullSegments; iSegment++)
                        {
                            byte[] buffer = new byte[MaxSegSize];
                            int position = iSegment * MaxSegSize;
                            ms.Read(buffer, 0, MaxSegSize);
                            stamp_segment stamp_segment = conciergeEntities.stamp_segment.CreateObject();
                            stamp_segment.id = stamp_id;
                            stamp_segment.data = buffer;
                            stamp_segment.position = iSegment;
                            conciergeEntities.stamp_segment.AddObject(stamp_segment);
                        }
                        if (leftOver > 0)
                        {
                            byte[] buffer = new byte[leftOver];
                            int position = (int)(ms.Length - leftOver);
                            ms.Read(buffer, 0, leftOver);
                            stamp_segment stamp_segment = conciergeEntities.stamp_segment.CreateObject();
                            stamp_segment.id = stamp_id;
                            stamp_segment.data = buffer;
                            stamp_segment.position = fullSegments;
                            conciergeEntities.stamp_segment.AddObject(stamp_segment);
                        }
                        conciergeEntities.SaveChanges();
                        pat.signature_document_id = stamp_id;
                        conciergeEntities.SaveChanges();
                        transaction.Commit();
                        conciergeEntities.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        */
        public Hashtable GetSearchEngines()
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            List<Hashtable> dataResults = new List<Hashtable>();
            result["data"] = dataResults;
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    var query = from se in ent.search_engine
                                select se;
                    foreach (search_engine se in query)
                    {
                        Hashtable entry = new Hashtable();
                        entry["name"] = se.name;
                        entry["query_string"] = se.query_string;
                        dataResults.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable AddLocation(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                using (conciergeEntities ent = new conciergeEntities())
                {
                    location location = ent.location.CreateObject();
                    location.name = values["name"] as string;
                    location.address1 = values["address1"] as string;
                    location.address2 = values["address2"] as string;
                    location.address3 = values["address3"] as string;
                    location.city = values["city"] as string;
                    location.contact_person = values["contact_person"] as string;
                    location.country = values["country"] as string;
                    location.email = values["email"] as string;
                    location.fax = values["fax"] as string;
                    location.locality1 = values["locality1"] as string;
                    location.locality2 = values["locality2"] as string;
                    location.postal_code = values["postal_code"] as string;
                    location.telephone = values["telephone"] as string;
                    ent.location.AddObject(location);
                    ent.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        public Hashtable AddActivities(List<Hashtable> items)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            List<Hashtable> dataResults = new List<Hashtable>();
            result["data"] = dataResults;
            string fileName = string.Empty;
            conciergeEntities conciergeEntities;
            using (conciergeEntities = new conciergeEntities())
            {
                conciergeEntities.Connection.Open();
                using (DbTransaction transaction = conciergeEntities.Connection.BeginTransaction())
                {
                    foreach (var item in items)
                    {
                        string specialty = (item["specialty"] as string).Trim().ToLower();
                        string target_name = (item["target_name"] as string).Trim().ToLower();
                        int patient_id = (int)item["patient_id"];
                        string fullFileName = item["path"] as string;
                        FileInfo fileInfo = new FileInfo(fullFileName);
                        fileName = fileInfo.Name;
                        int doctor_id = (int)item["doctor_id"];
                        DateTime dt;
                        Match m;
                        dt = (DateTime)item["procedureDate"];
                        string procedure_name = item["procedure_name"] as string;
                        string location_name = item["location_name"] as string; ;
                        var dpQuery0 = from dp in conciergeEntities.doctor_patient
                                      where dp.patient_id == patient_id && dp.doctor_id == doctor_id
                                      select dp;
                        doctor_patient doctor_patient;
                        if (dpQuery0.Count() == 0)
                        {
                            doctor_patient = conciergeEntities.doctor_patient.CreateObject();
                            doctor_patient.doctor_id = doctor_id;
                            doctor_patient.patient_id = patient_id;
                            conciergeEntities.doctor_patient.AddObject(doctor_patient);
                            conciergeEntities.SaveChanges();
                        }
                        doctor_patient = dpQuery0.First();
                        int doctor_patient_id = doctor_patient.id;

                        //int file_id = AddFile_(fileInfo, conciergeEntities);
                        Hashtable r = AddFile(fileInfo, conciergeEntities);
                        int file_id = (int)r["file_id"];
                        conciergeEntities.SaveChanges();

                        var dpQuery = from dp in conciergeEntities.doctor_patient
                                      where dp.doctor_id == doctor_id && dp.patient_id == patient_id
                                      select dp;
                        var dsQuery = from ds in conciergeEntities.doctor_specialty
                                      where ds.doctor_id == doctor_id && ds.specialty_name.Trim().ToLower() == specialty
                                      select ds;
                        doctor_specialty dr_spe;
                        switch (dsQuery.Count())
                        {
                            case 0:
                                {
                                    dr_spe = conciergeEntities.CreateObject<doctor_specialty>();
                                    dr_spe.specialty_name = specialty;
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
                        int procedure_target_id;
                        var ptQuery = from pt in conciergeEntities.procedure_target
                                      where pt.procedure_name.Trim().ToLower() == procedure_name && pt.target_name.Trim().ToLower() == target_name
                                      select pt;
                        procedure_target_id = ptQuery.First().id;
                        activity activity2 = conciergeEntities.activity.CreateObject();
                        activity2.procedure_specialty_id = (int)item["procedure_specialty_id"];
                        activity2.procedure_target_id = procedure_target_id;
                        activity2.doctor_specialty_id = dr_spe.id;
                        activity2.date = dt;
                        activity2.doctor_patient_id = doctor_patient_id;
                        activity2.location_name = location_name;
                        activity2.document_id = file_id;
                        //activity activity2 = activity.Createactivity(specialty_id,doctor_id,dt,patient_id,procedure_name,location_name,file_id);
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
                        catch (Exception )
                        {
                            throw;
                        }
                        Hashtable dataResultItem = new Hashtable();
                        dataResultItem["file_id"] = file_id;
                        dataResults.Add(dataResultItem);
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
                            activity.location_name = activities[i].location_name;
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
        static public Db Instance()
        {
            if (db == null)
            {
                db = new Db();
            }
            return db;
        }



























        private Hashtable AddFile(Hashtable values, conciergeEntities ent)
        {
            Hashtable result = new Hashtable();
            Hashtable result2;
            result["status"] = "ok";
            try
            {
                string data = values["data"] as string ?? "";
                string path = values["path"] as string ?? "";
                
                if (data == "" && path == "")
                    throw new Exception("data and path missing in AddFile");
                if (data != "" && path != "")
                    throw new Exception("data and path both specified in AddFile");
                result2 = GetServerDate(ent);
                if (result2["status"] as string != "ok")
                    return result2;
                DateTime dt = (DateTime)result2["date"];
                if (values["path"] != null)
                {
                    FileStream fs = new FileStream(values["path"] as string, FileMode.Open, FileAccess.Read);
                    return AddFile(fs, ent, "");
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
        // called by AddActivities only
        private Hashtable AddFile(FileInfo file, conciergeEntities ent)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
                return AddFile(fs, ent, file.FullName);
            }
            catch (Exception ex)
            {
                result["reason"] = Explain(ex);
                result["status"] = "error";
            }
            return result;
        }
        // No, these paramters are not redundant.  The s
        private Hashtable AddFile(Stream stream, conciergeEntities ent, string source)
        {
            Hashtable result = new Hashtable();
            Hashtable result2;
            result["status"] = "ok";

            try
            {
                string hash = FileHash(stream);
                result2 = GetServerDate(ent);
                if (result2["status"] as string != "ok")
                    return result2;
                document doc = ent.document.CreateObject();
                doc.path = source;
                doc.checksum = FileHash(stream);
                result2 = GetServerDate(ent);
                if (result2["status"] as string != "ok")
                    return result2;
                DateTime dt = (DateTime)result2["date"];
                doc.added_date = dt;
                ent.document.AddObject(doc);
                ent.SaveChanges();
                int id = (from d in ent.document
                          select d.id).Max();
                result["file_id"] = id;
                doc = (from d in ent.document
                       where d.id == id
                       select d).First();

                stream.Position = 0;
                int fullSegments = (int)(stream.Length / MaxDocumentSegmentSize);
                int leftOver = (int)(stream.Length % MaxDocumentSegmentSize);
                for (int iSegment = 0; iSegment < fullSegments; iSegment++)
                {
                    byte[] buffer = new byte[MaxDocumentSegmentSize];
                    int position = iSegment * MaxDocumentSegmentSize;
                    stream.Read(buffer, 0, MaxDocumentSegmentSize);
                    document_segment segment = ent.document_segment.CreateObject();
                    segment.document_id = id;
                    segment.data = buffer;
                    segment.position = iSegment;
                    ent.document_segment.AddObject(segment);
                }
                ent.SaveChanges();
                if (leftOver > 0)
                {
                    byte[] buffer = new byte[leftOver];
                    int position = (int)(stream.Length - leftOver);
                    stream.Read(buffer, 0, leftOver);
                    document_segment segment = ent.document_segment.CreateObject();
                    segment.document_id = id;
                    segment.data = buffer;
                    segment.position = fullSegments;
                    ent.document_segment.AddObject(segment);
                }
                ent.SaveChanges();
            }
            catch (Exception ex)
            {
                result["reason"] = Explain(ex);
                result["status"] = "error";
            }
            return result;
        }

        public Hashtable AddFile(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try 
	        {	        
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    using (DbTransaction txn = ent.Connection.BeginTransaction())
                    {   /*
                        if (values["name"] as string != "" && values["patient_id"] == null)
                        {
                            throw new Exception("name but not patient specified when adding file");
                        }
                        if (values["name"] as string == "" && values["patient_id"] != null)
                        {
                            throw new Exception("patient but not name specified when adding file");
                        }*/
                        result = AddFile(values, ent);
                        ent.SaveChanges();
                        if (result["status"] as string != "ok")
                        {
                            txn.Rollback();
                            return result;
                        }
                        int document_id = (int)result["file_id"];
                        /*
                        if (values["patient_id"] != null)
                        {
                            int patient_id = (int)values["patient_id"];
                            var query = from p in ent.patient
                                        where p.id == patient_id
                                        select p;
                            if (query.Count() == 0)
                                throw new Exception("patient not found while adding file");
                            patient p2 = query.First();
                            if (p2.signature_document_id != null)
                                throw new Exception("patient already has a signature");
                            p2.signature_document_id = (int)result["document_id"];
                            ent.SaveChanges();
                        }
                         * */
                        string name = values["name"] as string ;
                        if (name != "")
                        {
                            var query = from sd in ent.special_document
                                        where sd.name.Trim().ToLower() == name.Trim().ToLower()
                                        select sd;
                            if (query.Count() > 0)
                                throw new Exception("Special file already exists");
                            special_document sd2 = ent.special_document.CreateObject();
                            sd2.name = name;
                            sd2.document_id = document_id;
                            ent.special_document.AddObject(sd2);
                            ent.SaveChanges();
                        }
                        ent.SaveChanges();
                        txn.Commit();
                    }
                }
	        }
	        catch (Exception ex)
	        {
                result["status"] = "error";
                result["reason"] = Explain(ex);
	        }
            return result;
            /*
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                string name = ((values["name"] as string) ?? "").Trim();
                string path = ((values["path"] as string) ?? "").Trim();
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    using (DbTransaction txn = ent.Connection.BeginTransaction())
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi == null)
                            throw new Exception("Bad path");
                        int i = AddFile_(fi, ent);
                        ent.SaveChanges();
                        if (name != string.Empty)
                        {
                            special_document sd = ent.special_document.CreateObject();
                            sd.document_id = i;
                            sd.name = name;
                            ent.special_document.AddObject(sd);
                        }
                        ent.SaveChanges();
                        txn.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;*/
        }
        public Hashtable GetDoctorsForPatient(Hashtable values)
        {
            Hashtable result = new Hashtable();
            result["status"] = "ok";
            try
            {
                int patient_id = (int)values["patient_id"];
                using (conciergeEntities ent = new conciergeEntities())
                {
                    ent.Connection.Open();
                    var query1 = (from dp in ent.doctor_patient
                                  where dp.patient_id == patient_id
                                  select dp);
                    List<Hashtable> items = new List<Hashtable>();
                    foreach (doctor_patient dp in query1)
                    {
                        Hashtable item = new Hashtable();
                        item["doctor_patient_id"] = dp.id;
                        item["doctor_id"] = dp.doctor_id;
                    }
                    result["data"] = items;
                }
            }
            catch (Exception ex)
            {
                result["status"] = "error";
                result["reason"] = Explain(ex);
            }
            return result;
        }
    }
}
