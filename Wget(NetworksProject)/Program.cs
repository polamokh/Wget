using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.ComponentModel;

namespace Wget_NetworksProject_
{
    class Program
    {

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("== Wget Console == \n[3rd Year SWE'18]");

            string command;
            string[] arguments;
            string sURL;
            string tempURL = "";
            string[] urlIndex;
            string fileName;
            string desDir;

            do
            {
                Console.Write('\n' + Environment.UserName + '>');
                command = Console.ReadLine();

                arguments = command.Split(' ');

                if (command == "")
                    continue;
                //close the Wget program
                else if (command == "exit")
                    return;
                //Clear console text
                else if (command == "clear") 
                {
                    Console.Clear(); continue;
                }

                if (arguments.Length < 3 || arguments[0] != "wget")
                {
                    Console.WriteLine("Invalid Wget command or number of arguments, Please try again.. Example: wget [url] [des_dir]"); continue;
                }

                //Get the second argument that contain the URL
                sURL = arguments[1];

                checkagain:
                urlIndex = sURL.Split('/');

                //Check if the URL is http to open port 80
                if (urlIndex[0].ToLower() != "http:") 
                {
                    Console.WriteLine("Invalid URL maybe it is not a http.\nPlease try again.."); continue;
                }

                //To extract the file in html extension or as in link
                if (urlIndex[urlIndex.Length - 1] == "" && !urlIndex[urlIndex.Length - 2].Contains('.'))
                    fileName = urlIndex[urlIndex.Length - 2] + ".html";
                else if (urlIndex[urlIndex.Length - 1] != "" && !urlIndex[urlIndex.Length - 1].Contains('.'))
                    fileName = urlIndex[urlIndex.Length - 1] + ".html";
                //-----------------------------------------------------
                else
                    fileName = urlIndex[urlIndex.Length - 1];

                //Get website name to use in GET HTTP request
                string dnsName = urlIndex[2];
                //Get filename and where in website to use in GET HTTP request
                string requestFile = sURL.Substring(7 + dnsName.Length);

                //The directory folder the file will be saved in it
                desDir = arguments[2];
                if (!Directory.Exists(desDir))
                {
                    Console.WriteLine("Invalid directory maybe the path not exists or contains spaces.\nPlease try again.."); continue;
                }

                //Connect to the server by IPv4 TCP Connection
                Socket clientSK = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //Get DNS IPs
                IPHostEntry hostEntry = Dns.GetHostEntry(dnsName);

                //Get one IP if there is more one to the website and connect to the server
                foreach (var address in hostEntry.AddressList)
                {
                    IPEndPoint serverEP = new IPEndPoint(address, 80);

                    clientSK.Connect(serverEP);
                    if (clientSK.Connected)
                        break;
                    else
                        continue;
                }

                //Format of HTTP GET Request
                string request = "GET " + requestFile + " HTTP/1.0\r\nHost: " + dnsName + "\r\nConnection: Close\r\n\r\n"; //GET HTTP Request
                byte[] bytesSend = Encoding.ASCII.GetBytes(request);
                byte[] bytesReceived = new byte[1024];

                //Send and Receive to/from the server
                using (clientSK)
                {
                    if (clientSK == null)
                        Console.WriteLine("Connection failed.");

                    //Send GET HTTP request to the server
                    clientSK.Send(bytesSend);

                    //Get Server Response and header details
                    int receivedLength;
                    bool firstTime = false;
                    int resultFlag = 0;
                    double fileSize = 0;
                    double fileSizeDownloaded = 0;
                    var downloadedFile = File.Create(desDir + '\\' + fileName);
                    while ((receivedLength = clientSK.Receive(bytesReceived)) > 0) //Receive bytes from server
                    {
                        int indexOfEOH = 0;
                        if (!firstTime)
                        {
                            string recStr = Encoding.ASCII.GetString(bytesReceived, 0, receivedLength);
                            indexOfEOH = recStr.IndexOf("\r\n\r\n") + 4; //End of HTTP response header(index of it)
                            string[] recHttp = recStr.Split('\n');
                            if (recHttp[0].Contains("200 OK"))
                                resultFlag = 0;
                            else if (recHttp[0].Contains("301 Moved Permanently"))
                                resultFlag = 1;
                            else
                                resultFlag = 2;

                            int ptrHeader = 0;
                            for (int i = 0; ; i++)
                            {
                                Console.WriteLine(recHttp[i]);
                                ptrHeader += recHttp[i].Length + 1;
                                if (ptrHeader >= indexOfEOH)
                                    break;

                                //Get new the URL if the file is moved
                                if (recHttp[i].Contains("Location:"))
                                {
                                    string[] temp = recHttp[i].Split(' ');
                                    tempURL = temp[1].Remove(temp[1].Length - 1);
                                }

                                //Get the file size
                                if (recHttp[i].Contains("Content-Length:"))
                                {
                                    string[] temp = recHttp[i].Split(' ');
                                    fileSize = Convert.ToUInt32(temp[1]);
                                }
                            }
                            firstTime = true;
                        }

                        //If Resp. is OK from http header
                        if (resultFlag == 0)
                        {
                            downloadedFile.Write(bytesReceived, indexOfEOH, receivedLength - indexOfEOH);
                            fileSizeDownloaded += Convert.ToUInt32(receivedLength);
                            if (fileSize != 0)
                                Console.Write("\rDownloading file \"" + fileName + "\"... {0} KB from {1} KB",
                                    (fileSizeDownloaded / 1024d).ToString("0.00"),
                                    (fileSize / 1024d).ToString("0.00"));
                            else
                                Console.Write("\rDownloading file \"" + fileName + "\"... {0} KB",
                                    (fileSizeDownloaded / 1024d).ToString("0.00"));
                        }
                        //If Resp. is Moved from http header
                        else if (resultFlag == 1)
                        {
                            downloadedFile.Close();
                            File.Delete(desDir + '\\' + fileName);
                            sURL = tempURL;
                            Console.WriteLine("Check again with new URL: " + sURL);
                            goto checkagain;
                        }
                        //If Resp. are Bad Request or Not Found
                        else
                        {
                            downloadedFile.Close();
                            File.Delete(desDir + '\\' + fileName);
                            break;
                        }
                    }

                    //File Downloaded Successfully
                    if (resultFlag == 0 && fileSizeDownloaded >= fileSize)
                    {
                        downloadedFile.Close();
                        Console.WriteLine("\nFile Downloaded Successfully \"" + fileName + "\"\nSize: {0} KB", (fileSizeDownloaded / 1024d).ToString("0.00"));
                        Process.Start(desDir + '\\' + fileName);
                    }
                    //File Not Downloaded Successfully we delete the file
                    else if (resultFlag == 0 && fileSizeDownloaded < fileSize)
                    {
                        downloadedFile.Close();
                        Console.WriteLine("\nThere is an error while downloading your file please check your internet connection.");
                        File.Delete(desDir + '\\' + fileName);
                    }
                    
                    clientSK.Close();
                }
            } while (true);
        }
    }
}
