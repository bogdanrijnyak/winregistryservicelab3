/*
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WS3
{
    public partial class Service1 : ServiceBase
    {
        private static void correctFormat(ref List<string> stringList)
        {
            Regex NV = new Regex("^New Value #\\d+$");
            Regex R = new Regex("^Task_\\d{4}$");
            for (int i = 0; i < stringList.Count; i++)
            {
                if (NV.Match(stringList[i]).Success == true)
                {
                    stringList.Remove(stringList[i]);
                    i--;
                }
                else if (R.Match(stringList[i]).Success == false)
                {
                    File.Delete(@"C:\Task_Queue\Claims\" + stringList[i] + ".txt");
                    vClaims = d.GetFiles();
                    WriteLog($"Incorrect syntax of claim -> {stringList[i]}");
                    stringList.Remove(stringList[i]);
                    i--;
                    toSkip = true;
                }
            }
        }
        private static void processClaim(List<string> stringList)
        {
            string taskToProcess = stringList.First();
            File.Delete(@"C:\Task_Queue\Claims\" + taskToProcess + ".txt");
            vClaims = new DirectoryInfo(@"C:\Task_Queue\Claims").GetFiles("*");
            using (StreamWriter F = new StreamWriter(File.Open(@"C:\Task_Queue\Tasks\" + taskToProcess + "-[....................]-Queued.txt", FileMode.Create)))
            {
                F.WriteLine(DateTime.Now + " Claim added!\n");
            }
            vTasks.Add(taskToProcess + "-[....................]-Queued");
            WriteLog($"the application {taskToProcess} was successfully accepted for processing");
        }

        static private RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        private static DirectoryInfo d = new DirectoryInfo(@"C:\Task_Queue\Claims");
        private static DirectoryInfo d2 = new DirectoryInfo(@"C:\Task_Queue\Tasks");
        private static FileInfo[] vClaims = d.GetFiles("*");
        private static List<string> vTasks = d2.GetFiles("*").ToList().ConvertAll(x => x.Name.Replace(".txt", ""));
        static bool running = true, toSkip = false;
        private static void claimsHandler()
        {
            List<string> tasks = new List<string>();
            int claimTime;
            do
            {
                vClaims = new DirectoryInfo(@"C:\Task_Queue\Claims").GetFiles("*");
                using (RegistryKey tmp = hklm.OpenSubKey(@"SOFTWARE\Task_Queue\Parameters", true))
                {
                    claimTime = (int)tmp.GetValue("Task_Claim_Check_Period", 30);
                }
                //claimTime = 30;
                if (vClaims.Length > 0)
                {
                    tasks.Clear();
                    foreach (var task in vClaims)
                        tasks.Add(task.Name.Replace(".txt", ""));

                    correctFormat(ref tasks);

                    if (tasks.Count > 0 || toSkip == false)
                    {
                        processClaim(tasks);
                    }
                    toSkip = false;
                    System.Threading.Thread.Sleep(claimTime * 1000);
                }
            } while (running);
        }

        private static void updateWholeTasksList()
        {
            vTasks = d2.GetFiles("*").ToList().ConvertAll(x => x.Name.Replace(".txt", ""));
            string[] gvNames = vTasks.ToArray();
            allTasks.Clear();
            foreach (var task in gvNames)
                allTasks.Add(task);
        }
        private static void updateParametersValues()
        {
            using (RegistryKey tmp = hklm.OpenSubKey(@"SOFTWARE\Task_Queue\Parameters", true))
            {
                taskTime = (int)tmp.GetValue("Task_Execution_Duration", 60);
                quantity = (int)tmp.GetValue("Task_Execution_Quantity", 1);
            }
            //taskTime = 10;
            //quantity = 3;
        }
        private static List<string> getUncompletedTasks()
        {
            List<string> uncTasks = new List<string>();
            foreach (var task in allTasks)
            {
                if (task.Contains("COMPLETED") == false) uncTasks.Add(task);
            }
            return uncTasks;
        }
        private static void orderTasks(ref List<string> tasks)
        {
            List<string> newTasks = new List<string>(), temp = new List<string>();
            foreach (var task in tasks)
            {
                if (task.Contains("In progress"))
                {
                    newTasks.Add(task);
                }
                else temp.Add(task);
            }
            newTasks.Sort(); newTasks.Reverse();
            temp.Sort(); temp.Reverse();
            foreach (var tempTask in temp)
            {
                newTasks.Add(tempTask);
            }
            tasks = newTasks;
        }
        private static string getCurrentStringName(string targetTask)
        {
            string tmpTask = "";
            foreach (var task in allTasks)
                if (targetTask.Substring(0, 9) == task.Substring(0, 9))
                    tmpTask = task;
            return tmpTask;
        }
        private static void updateTaskProgress(string task)
        {
            string currentStringName = getCurrentStringName(task);
            File.Move(@"C:\Task_Queue\Tasks\" + currentStringName + ".txt", @"C:\Task_Queue\Tasks\" + task + ".txt");
            if (task.Contains("COMPLETED"))
            {
                uncompletedTasks.Remove(task);
            }
        }
        private static float getPercentageProgress(string task)
        {
            if (task.Contains("Queued"))
            {
                return 0;
            }
            else
            {
                return float.Parse(task.Split('%')[0].Split(' ')[task.Split('%')[0].Split(' ').Count() - 1]);
            }
        }
        private static void updateLoadingString(ref string task)
        {
            float newPercentage = percentage + progressToAdd;
            if (newPercentage >= 25 && newPercentage < 50)
            {
                task = task.Remove(11, 5).Insert(11, "IIIII");
            }
            else if (newPercentage >= 50 && newPercentage < 75)
            {
                task = task.Remove(11, 10).Insert(11, "IIIIIIIIII");
            }
            else if (newPercentage >= 75 && newPercentage < 100)
            {
                task = task.Remove(11, 15).Insert(11, "IIIIIIIIIIIIIII");
            }
            else if (newPercentage >= 100)
            {
                task = task.Remove(11, 20).Insert(11, "IIIIIIIIIIIIIIIIIIII");
            }
        }
        private static void processTask(ref string task)
        {
            updateLoadingString(ref task);

            if ((percentage + progressToAdd) >= 100)
            {
                WriteLog($"{task.Substring(0, 9)} was successfully executed!");

                task = task.Replace($"In progress - {percentage}% completed", "COMPLETED");
            }
            else if (percentage == 0)
            {
                task = task.Replace($"Queued", $"In progress - 1% completed");
            }
            else
            {
                task = task.Replace($"In progress - {percentage}% completed", $"In progress - {percentage + progressToAdd}% completed");
            }
        }
        private static void processTasks()
        {
            for (int i = 0; i < quantity; i++)
            {
                if (uncompletedTasks.Count > 0 && i < uncompletedTasks.Count())
                {
                    string tmp = uncompletedTasks[i];
                    percentage = getPercentageProgress(tmp);
                    progressToAdd = (float)100 / (float)taskTime;
                    processTask(ref tmp);
                    uncompletedTasks[i] = tmp;
                    updateTaskProgress(tmp);
                }
            }
            System.Threading.Thread.Sleep(2000);
        }

        static private int taskTime = 60, quantity = 1;
        static private float progressToAdd, percentage;
        static private List<string> uncompletedTasks = new List<string>();
        static List<string> allTasks = new List<string>();
        private static void tasksHandler()
        {
            do
            {
                updateParametersValues();
                updateWholeTasksList();

                uncompletedTasks = getUncompletedTasks();

                processTasks();
            } while (running);
        }
        public Service1()
        {
            InitializeComponent();
        }
        System.Threading.Thread TH1 = new System.Threading.Thread(claimsHandler);
        System.Threading.Thread TH2 = new System.Threading.Thread(tasksHandler);
        protected override void OnStart(string[] args)
        {
            running = true;
            TH1.Start();
            TH2.Start();
        }
        protected override void OnStop()
        {
            running = false;
        }
        private static void WriteLog(string z)
        {
            using (StreamWriter F = new StreamWriter("C:\\Logs\\TaskQueue_18-11-2013.log", true))
            {
                F.WriteLine(DateTime.Now + " " + z);
            }
        }
    }
}
