﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Web.Services;
using umbraco.cms.businesslogic.web;

namespace umbraco.webservices.documents
{

    /// <summary>
    /// Service managing documents in umbraco
    /// </summary>
    [WebService(Namespace = "http://umbraco.org/webservices/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    public class documentService : BaseWebService
    {

        override public Services Service
        {
            get
            {
                return Services.DocumentService;
            }
        }

        [WebMethod]
        public int create(documentCarrier carrier, string username, string password)
        {
            Authenticate(username, password);

            // Some validation
            if (carrier == null) throw new Exception("No carrier specified");
            if (carrier.ParentID == 0) throw new Exception("Document needs a parent");
            if (carrier.DocumentTypeID == 0) throw new Exception("Documenttype must be specified");
            if (carrier.Id != 0) throw new Exception("ID cannot be specifed when creating. Must be 0");
            if (carrier.Name == null || carrier.Name.Length == 0) carrier.Name = "unnamed";

            umbraco.BusinessLogic.User user = GetUser(username, password);

            // We get the documenttype
            umbraco.cms.businesslogic.web.DocumentType docType = new umbraco.cms.businesslogic.web.DocumentType(carrier.DocumentTypeID);
            if (docType == null) throw new Exception("DocumenttypeID " + carrier.DocumentTypeID + " not found");

            // We create the document
            Document newDoc = Document.MakeNew(carrier.Name, docType, user, carrier.ParentID);
            newDoc.ReleaseDate = carrier.ReleaseDate;
            newDoc.ExpireDate = carrier.ExpireDate;

            // We iterate the properties in the carrier
            if (carrier.DocumentProperties != null)
            {
                foreach (documentProperty updatedproperty in carrier.DocumentProperties)
                {
                    umbraco.cms.businesslogic.property.Property property = newDoc.getProperty(updatedproperty.Key);
                    if (property == null) throw new Exception("property " + updatedproperty.Key + " was not found");
                    property.Value = updatedproperty.PropertyValue;
                }
            }
            newDoc.Save();
            // We check the publishaction and do the appropiate
            handlePublishing(newDoc, carrier, user);

            // We return the ID of the document..65
            return newDoc.Id;
        }

        [WebMethod]
        public documentCarrier read(int id, string username, string password)
        {
            Authenticate(username, password);

            umbraco.cms.businesslogic.web.Document doc = null;

            try
            {
                doc = new umbraco.cms.businesslogic.web.Document(id);
            }
            catch
            { }

            if (doc == null)
                throw new Exception("Could not load Document with ID: " + id);

            documentCarrier carrier = createCarrier(doc);
            return carrier;
        }

        [WebMethod]
        public documentCarrier ReadPublished(int id, string username, string password)
        {
            Authenticate(username, password);

            var doc = new Document(id, true);

            var publishedDoc = doc.GetPublishedVersion();

            doc = publishedDoc == null ? new Document(id) : new Document(id, publishedDoc.Version);
            
            if (doc == null)
                throw new Exception("Could not load Document with ID: " + id);

            return createCarrier(doc);
        }

        [WebMethod]
        public List<documentCarrier> readList(int parentid, string username, string password)
        {
            Authenticate(username, password);

            umbraco.cms.businesslogic.web.Document[] docList;
            umbraco.cms.businesslogic.web.Document doc = null;
            List<documentCarrier> carriers = new List<documentCarrier>();

            if (parentid == 0)
            {
                docList = Document.GetRootDocuments();
            }
            else
            {
                try
                {
                    doc = new umbraco.cms.businesslogic.web.Document(parentid);
                }
                catch
                { }

                if (doc == null)
                    throw new Exception("Parent document with ID " + parentid + " not found");

                try
                {
                    if (!doc.HasChildren)
                        return carriers;

                    docList = doc.Children;
                }
                catch (Exception exception)
                {
                    throw new Exception("Could not load children: " + exception.Message);
                }
            }

            // Loop the nodes in docList
            foreach (Document childdoc in docList)
            {
                carriers.Add(createCarrier(childdoc));
            }
            return carriers;
        }


        [WebMethod]
        public void update(documentCarrier carrier, string username, string password)
        {
            Authenticate(username, password);

            if (carrier.Id == 0) throw new Exception("ID must be specifed when updating");
            if (carrier == null) throw new Exception("No carrier specified");

            umbraco.BusinessLogic.User user = GetUser(username, password);

            Document doc = null;
            try
            {
                doc = new Document(carrier.Id);
            }
            catch { }
            if (doc == null)



                // We assign the new values:
                doc.ReleaseDate = carrier.ReleaseDate;
            doc.ExpireDate = carrier.ExpireDate;
            if (carrier.ParentID != 0)
            {
                doc.Move(carrier.ParentID);
            }

            if (carrier.Name.Length != 0)
            {
                doc.Text = carrier.Name;
            }

            // We iterate the properties in the carrier
            if (carrier.DocumentProperties != null)
            {
                foreach (documentProperty updatedproperty in carrier.DocumentProperties)
                {
                    umbraco.cms.businesslogic.property.Property property = doc.getProperty(updatedproperty.Key);

                    if (property == null)
                    {
                    }
                    else
                    {
                        property.Value = updatedproperty.PropertyValue;
                    }
                }
            }
            doc.Save();
            handlePublishing(doc, carrier, user);
        }


        [WebMethod]
        public void delete(int id, string username, string password)
        {
            Authenticate(username, password);

            // Some validation, to prevent deletion of system-documents.. (nessecary?)
            if (id < 0)
            {
                throw new Exception("Cannot delete documents with id lower than 1");
            }

            // We load the document
            Document doc = null;
            try
            {
                doc = new Document(id);
            }
            catch
            { }

            if (doc == null)
                throw new Exception("Document not found");

            try
            {
                doc.delete(true);                
            }
            catch (Exception ex)
            {
                throw new Exception("Document could not be deleted" + ex.Message);
            }
        }


        private void handlePublishing(Document doc, documentCarrier carrier, umbraco.BusinessLogic.User user)
        {
            switch (carrier.PublishAction)
            {
                case documentCarrier.EPublishAction.Publish:
                    if (doc.PublishWithResult(user))
                    {
                        umbraco.library.UpdateDocumentCache(doc);
                    }
                    break;
                case documentCarrier.EPublishAction.Unpublish:
                    if (doc.PublishWithResult(user))
                    {
                        umbraco.library.UnPublishSingleNode(doc);
                    }
                    break;
                case documentCarrier.EPublishAction.Ignore:
                    if (doc.Published)
                    {
                        if (doc.PublishWithResult(user))
                        {
                            umbraco.library.UpdateDocumentCache(doc);
                        }
                    }
                    else
                    {
                        if (doc.PublishWithResult(user))
                        {
                            umbraco.library.UpdateDocumentCache(doc);
                        }
                    }
                    break;
            }
        }


        private documentCarrier createCarrier(Document doc)
        {
            documentCarrier carrier = new documentCarrier();
            carrier.ExpireDate = doc.ExpireDate;
            carrier.ReleaseDate = doc.ReleaseDate;
            carrier.Id = doc.Id;
            carrier.Name = doc.Text;

            try
            {
                carrier.ParentID = doc.Parent.Id;
            }
            catch
            {
            }

            carrier.Published = doc.Published;
            carrier.HasChildren = doc.HasChildren;
            var props = doc.getProperties;
            foreach (umbraco.cms.businesslogic.property.Property prop in props)
            {
                documentProperty carrierprop = new documentProperty();

                if (prop.Value == System.DBNull.Value)
                {
                    carrierprop.PropertyValue = null;
                }
                else
                {
                    carrierprop.PropertyValue = prop.Value;
                }

                carrierprop.Key = prop.PropertyType.Alias;
                carrier.DocumentProperties.Add(carrierprop);
            }
            return carrier;

        }

    }
}
