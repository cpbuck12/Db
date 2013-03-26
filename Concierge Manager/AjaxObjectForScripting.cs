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
        public bool PdfReply(byte[] payload, IHttpResponse response)
        {
            if (payload == null || payload.Length <= 0)
            {
                response.Status = HttpStatusCode.NotFound;
                return false;
            }
            response.Body.Write(payload, 0, payload.Length);
            response.Status = HttpStatusCode.OK;
            response.ContentType = "application/pdf";
            return true;
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
                if (fs != null)
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
            if (request.Body.Length == 0)
                return null;
            request.Body.Position = 0;
            XmlReader reader = JsonReaderWriterFactory.CreateJsonReader(request.Body, new System.Xml.XmlDictionaryReaderQuotas());
            XElement root = XElement.Load(reader);
            return root;
        }
        public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
        {
 //           XElement xmlRequest0 = GetXmlRequestPayload(request);
//            AjaxReply(objectForScripting.CreateWebsite(xmlRequest0), response);
            if (request.UriParts.Length == 0)
            {
                string[] files = { "work.htm" };
                return GetFile(files, response);
            }
            else if (request.UriParts[0].ToLower() == "downloadfile")
            {
                int fileId = int.Parse(request.UriParts[1].Split(new char[] { '.' })[0]);
                return PdfReply(objectForScripting.DownloadFile(fileId),response);
            }
            else if (request.UriParts[0].ToLower() == "ajax")
            {
                try
                {
                    XElement xmlRequest = GetXmlRequestPayload(request);

                    switch (request.UriParts[1])
                    {
                        case "DeleteDocument":
                            AjaxReply(objectForScripting.DeleteDocument(xmlRequest), response);
                            return true;
                        case "CreateWebsite":
                            AjaxReply(objectForScripting.CreateWebsite(xmlRequest), response);
                            return true;
                        case "SetCurrentDirectory":
                            AjaxReply(objectForScripting.SetCurrentDirectory(xmlRequest), response);
                            return true;
                        case "AddSpecialty":
                            AjaxReply(objectForScripting.AddSpecialty(xmlRequest), response);
                            return true;
                        case "AddPatient":
                            AjaxReply(objectForScripting.AddPatient(xmlRequest), response);
                            return true;
                        case "UpdateDoctor":
                            AjaxReply(objectForScripting.UpdateDoctor(xmlRequest), response);
                            return true;
                        case "GetDoctors":
                            AjaxReply(objectForScripting.GetDoctors(), response);
                            return true;
                        case "AddDoctor":
                            AjaxReply(objectForScripting.AddDoctor(xmlRequest), response);
                            return true;
                        case "AddActivities":
                            AjaxReply(objectForScripting.AddActivities(xmlRequest), response);
                            return true;
                        case "UploadFile":
                            AjaxReply(objectForScripting.UploadFile(xmlRequest), response);
                            return true;
                        case "GetPeopleOnDisk":
                            AjaxReply(objectForScripting.GetPeopleOnDisk(), response);
                            return true;
                        case "GetFilesOnDisk":
                            AjaxReply(objectForScripting.GetFilesOnDisk(xmlRequest), response);
                            return true;
                        case "GetPeopleInDb":
                            AjaxReply(objectForScripting.GetPatients(), response);
                            return true;
                        case "GetSpecialties":
                            AjaxReply(objectForScripting.GetSpecialties(), response);
                            return true;
                        case "BrowseDocuments":
                            AjaxReply(objectForScripting.BrowseDocuments(), response);
                            return true;
                    }
                    return false; // TODO finish this
                }
                catch (Exception)
                {
                    return false; // TODO: error handling in more depth
                }
            }
            else
            {
                return GetFile(request.UriParts, response); // regular stuff, like index.html
            }
        }
    }
}