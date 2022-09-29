// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage;
using Renci.SshNet;
using ConnectionInfo = Renci.SshNet.ConnectionInfo;
using static System.Net.WebRequestMethods;
using File = System.IO.File;
using System.Reflection.Metadata;

namespace EventgridCheck
{
    public static class EventGridfn
    {
        [FunctionName("EventGridTriggercheck")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            try
            {
                var EventData = eventGridEvent.Data;
                log.LogInformation(EventData.ToString());

                // Parsing the JSON Object
                var jo = JObject.Parse(EventData.ToString());
                var id = jo["url"].ToString();
                var storageUrl = id.ToString();
                log.LogInformation(storageUrl.ToString());

                // Split the Storage Url to get blob and container Name
                string[] splitString = Regex.Split(storageUrl, @"/");
                string Uri = splitString[2];
                string containername = splitString[3];
                string blobname = splitString[4];
                log.LogInformation("Storage Container:" + containername.ToString());
                log.LogInformation("Container Blob:" + blobname.ToString());

                // Get the Container Name
                string uploadedcontainer = Environment.GetEnvironmentVariable("archived");

                if (containername == uploadedcontainer)
                {
                    // Accessing blob using BlobService Client
                    var blobServiceClient = new BlobServiceClient(new Uri("https://" + Uri));
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containername);
                    BlobClient blobClient = containerClient.GetBlobClient(blobname);

                    // Storage account Details
                    var accountName = Environment.GetEnvironmentVariable("accountname");
                    var accountKey = Environment.GetEnvironmentVariable("accountkey");

                    var blobServiceEndpoint = "https://" + Uri;
                    // Creating SAS Token to access the blob
                    BlobAccountSasPermissions permissions = BlobAccountSasPermissions.Read;
                    var credential = new StorageSharedKeyCredential(accountName, accountKey);

                    var sas = new BlobSasBuilder
                    {
                        BlobName = blobname,
                        BlobContainerName = containername,
                        StartsOn = DateTimeOffset.UtcNow,
                        ExpiresOn = DateTime.UtcNow.AddHours(1)
                    };
                    sas.SetPermissions(permissions);

                    UriBuilder sasUri = new UriBuilder($"{blobServiceEndpoint}/{containername}/{blobname}");
                    sasUri.Query = sas.ToSasQueryParameters(credential).ToString();
                    log.LogInformation("SASURI:" + sasUri.Uri);
                    BlobClient blob = new BlobClient(sasUri.Uri);

                    // Downloading File from blob
                    var uploadedfile= blob.DownloadTo(@""+blobname);
                    log.LogInformation("Downloaded content"+ uploadedfile.ToString());
                    string filePath = @"" + blobname;

                    log.LogInformation("Blob is Uploaded to the Path");
                    // SFTP  key Details
                    PrivateKeyFile keyFile = new PrivateKeyFile(@"");

                    var keyFiles = new[] { keyFile };
                    //Host and UserName
                    string host = Environment.GetEnvironmentVariable("serveraddress");
                    string sftpUsername = Environment.GetEnvironmentVariable("sftpusername");
                    var methods = new List<AuthenticationMethod>();
                    methods.Add(new PrivateKeyAuthenticationMethod(sftpUsername, keyFiles));


                    // Connect to SFTP Server and Upload file 
                    ConnectionInfo con = new ConnectionInfo(host, 22, sftpUsername, methods.ToArray());
                    using (var client = new SftpClient(con))
                    {
                        client.Connect();
                        log.LogInformation("Connected to Sftp Client");
                        using (FileStream fs = new FileStream(filePath, FileMode.Open))
                        {
                            
                            client.UploadFile(fs, "");
                            log.LogInformation("File Uploaded Successfully..");

                            // Check the List of Files in Directory
                            var files = client.ListDirectory("");
                            foreach (var file in files)
                            {
                                log.LogInformation(file.Name);
                            }
                            client.Disconnect();
                        }



                    }

                    

                }
                //else if (containername == "samplecontainerabc")
                //{
                //    log.LogInformation("Blob is Uploaded to Sample container2");
                //}
                //else if (containername == "samplecontainerdef")
                //{
                //    log.LogInformation("Blob is Uploaded to Sample container3");
                //}
                else
                {
                    log.LogInformation("Invalid container");
                }

            }
            catch (Exception ex)
            {
                log.LogInformation(ex.ToString());
            }
        }
    }
}

