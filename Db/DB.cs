using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;

namespace Db
{
    public class Db
    {
        static private Db db = null;
        conciergeEntities conciergeEntities_;
        const int MaxDocumentSegmentSize = 0x4000;
        private Db()
        {
            conciergeEntities_ = new conciergeEntities();
            conciergeEntities_.Connection.Open();
        }
        public int[] FindFile(string hash)
        {
            document doc = conciergeEntities_.documents.CreateObject();
            var query = (from d in conciergeEntities_.documents
                         where d.checksum == hash
                         select d.id);
            return query.ToArray();
        }
        public patient[] Patients()
        {
            var foo = (from p in conciergeEntities_.patients
                       select p);
            return foo.ToArray();
        }
        public string FileHash(FileInfo file)
        {
            FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            SHA1Managed sha1 = new SHA1Managed();
            return string.Concat(from b in sha1.ComputeHash(stream)
                                 select b.ToString("X2"));
        }
        public void DumpFile(Stream stream, int id)
        {
            var segments = (from segment in conciergeEntities_.document_segment
                            where segment.document_id == id
                            orderby segment.position
                            select segment);
            foreach (var segment in segments)
            {
                stream.Write(segment.data, 0, segment.data.Length);
            }
        }
        public int[] DuplicateFiles(int id)
        {
            var documents = (from doc in conciergeEntities_.documents
                             join doc2 in conciergeEntities_.documents on doc.checksum equals doc2.checksum
                             where doc.id == id && doc2.id != id
                             select doc2.id);
            if (documents.Count() > 0)
                return documents.ToArray();
            else
                return new int[0];
        }
        public void RemoveFile(int id)
        {
            var query = (from d in conciergeEntities_.documents
                         where d.id == id
                         select d);
            var first = query.First();
            if (query.Count() == 0)
            {
                return; // doesn't exist
            }
            using (DbTransaction transaction = conciergeEntities_.Connection.BeginTransaction())
            {
                foreach (document_segment s in (from segment in conciergeEntities_.document_segment
                                                where segment.document_id == id
                                                select segment))
                {
                    conciergeEntities_.document_segment.DeleteObject(s);
                }
                conciergeEntities_.documents.DeleteObject(query.First());
                conciergeEntities_.SaveChanges();
                transaction.Commit();
            }
        }
        public int AddFile(FileInfo file)
        {
            int id;
            string hash = FileHash(file);

            using (DbTransaction transaction = conciergeEntities_.Connection.BeginTransaction())
            {
                document doc = conciergeEntities_.documents.CreateObject();
                doc.path = file.FullName;
                doc.checksum = "";
                conciergeEntities_.documents.AddObject(doc);
                conciergeEntities_.SaveChanges();
                id = (from d in conciergeEntities_.documents
                      select d.id).Max();
                doc = (from d in conciergeEntities_.documents
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
                    document_segment segment = conciergeEntities_.document_segment.CreateObject();
                    segment.document_id = id;
                    segment.data = buffer;
                    segment.position = iSegment;
                    conciergeEntities_.document_segment.AddObject(segment);
                }
                if (leftOver > 0)
                {
                    byte[] buffer = new byte[leftOver];
                    int position = (int)(fileStream.Length - leftOver);
                    fileStream.Read(buffer, 0, leftOver);
                    sha1.TransformFinalBlock(buffer, 0, leftOver);
                    document_segment segment = conciergeEntities_.document_segment.CreateObject();
                    segment.document_id = id;
                    segment.data = buffer;
                    segment.position = fullSegments;
                    conciergeEntities_.document_segment.AddObject(segment);
                }
                else
                    sha1.TransformFinalBlock(null, 0, 0);
                string hashKey = string.Concat(from b in sha1.Hash
                                               select b.ToString("X2"));
                doc.checksum = hashKey;
                doc.id = id;
                conciergeEntities_.SaveChanges();
                transaction.Commit();
            }
            return id;
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
