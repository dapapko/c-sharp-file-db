using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using Newtonsoft.Json;

namespace WpfApp1
{
    public class Record
    {
        public int Age { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public Dictionary<string, string> ToDict()
        {
            Dictionary<string, string> d = new Dictionary<string, string>
            {
                ["age"] = Age.ToString(),
                ["name"] = Name,
                ["phone"] = Phone
            };
            return d;
        }
        public string ToCSV()
        {
            return Name + "," + Phone + "," + Age.ToString();
        }
    }

    public class Database
    {
        private string rootdir;
        public Database(string root)
        {
            rootdir = root;
        }
        public void Init()
        {
            Directory.CreateDirectory(rootdir + "\\phones");
            Directory.CreateDirectory(rootdir + "\\names");
            Directory.CreateDirectory(rootdir + "\\entries");
        }
        public void Clear()
        {
            if (Directory.Exists(rootdir + "\\phones")) Directory.Delete(rootdir + "\\phones", true);
            if (Directory.Exists(rootdir + "\\names"))  Directory.Delete(rootdir + "\\names", true);
            if (Directory.Exists(rootdir + "\\entries"))  Directory.Delete(rootdir + "\\entries", true);
        }

       static void FlushDir(string path)
        {
            DirectoryInfo entries = new DirectoryInfo(path);

            foreach (FileInfo file in entries.EnumerateFiles())
            {
                file.Delete();
            }
        }
        static string Sha256(string randomString)
        {
            var crypt = new SHA256Managed();
            string hash = String.Empty;
            byte[] crypto = crypt.ComputeHash(Encoding.ASCII.GetBytes(randomString));
            foreach (byte theByte in crypto)
            {
                hash += theByte.ToString("x2");
            }
            return hash;
        }

        public void Flush()
        {
            FlushDir(rootdir + "\\entries");
            FlushDir(rootdir + "\\names");
            FlushDir(rootdir + "\\phones");

        }
        static void WriteTo(string filename, string content)
        {
            using (StreamWriter fs = new StreamWriter(filename))
            {
                fs.WriteLine(content);
            }
        }
        Boolean Exists(string fieldname, string value)
        {
            string hash = Sha256(value);
            string path = "";
            switch (fieldname)
            {

                case "name":
                    path = rootdir + "\\names\\" + hash;
                    break;
                case "phone":
                    path = rootdir + "\\phones\\" + hash;
                    break;

            }
            try
            {
                File.OpenRead(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Add(Record rec, string uuid = null)
        {
            if (Exists("phone", rec.Phone) || Exists("name", rec.Name))
            {
                throw new Exception("Record with such values of unique fields already exists");
            }
            if (uuid == null) uuid = Guid.NewGuid().ToString();
            string entryFilename = rootdir + "\\entries\\" + uuid;
            string nameFilename = rootdir + "\\names\\" + Sha256(rec.Name);
            string phoneFilename = rootdir + "\\phones\\" + Sha256(rec.Phone);
            string jsonString = JsonConvert.SerializeObject(rec);
            WriteTo(entryFilename, jsonString);
            WriteTo(nameFilename, uuid);
            WriteTo(phoneFilename, uuid);
        }
        public Record GetByPath(string path)
        {
            
                string entry = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Record>(entry);
           
        }

        public Record GetByID(string id)
        {
            string entryFilename = rootdir + "\\entries\\" + id.Trim('\r', '\n'); 
            return GetByPath(entryFilename);
        }

       public Record GetByUnique(string field, string value)
        {
            string path = "";
            string id;
            switch (field)
            {

                case "name":
                    path = rootdir + "\\names\\" + Sha256(value);
                    break;
                case "phone":
                    path = rootdir + "\\phones\\" + Sha256(value);
                    break;

            }
            try
            {
                id = File.ReadAllText(path);
            }
            catch (Exception)
            {
                throw new Exception("Record with such value of unique fields does not exist");
            }
            return GetByID(id);
        }

       public List<Record> SearchByAge(int age)
        {
            List<Record> records = new List<Record>();
            string[] entries = Directory.GetFiles(rootdir + "\\entries");
            foreach (string entry in entries)
            {
                Record rec = GetByPath(entry);
                if (rec.Age == age) records.Add(rec);
            }
            return records;
        }
        public List<string> GetAll()
        {
            List<string> records = new List<string>();
            string[] entries = Directory.GetFiles(rootdir + "\\entries");
            foreach (string entry in entries)
            {
                Record rec = GetByPath(entry);
                records.Add(rec.Name);
            }
            return records;
        }
        public void Remove(string id)
        {
            string path = rootdir + "/entries/" + id;
            Record rec = GetByPath(path);
            string nameFilename = rootdir + "\\names\\" + Sha256(rec.Name);
            string phoneFilename = rootdir + "\\phones\\" + Sha256(rec.Phone);
            File.Delete(path);
            File.Delete(nameFilename);
            File.Delete(phoneFilename);

        }

        public void DeleteByFieldValue(string field, string value)
        {
            string[] entries = Directory.GetFiles(rootdir + "/entries");
            foreach (string entry in entries)
            {
                Dictionary<string, string> rec = GetByPath(entry).ToDict();
                string nameFilename = rootdir + "\\names\\" + Sha256(rec["name"]);
                string phoneFilename = rootdir + "\\phones\\" + Sha256(rec["phone"]);
                if (field == "phone" || field == "name")
                {
                    if (rec[field].Equals(value))
                    {
                        File.Delete(entry);
                        File.Delete(nameFilename);
                        File.Delete(phoneFilename);
                    }
                    else
                    {
                        int age = Int32.Parse(value);
                        int recAge = Int32.Parse(rec["age"]);
                        if (age == recAge)
                        {
                            File.Delete(entry);
                            File.Delete(nameFilename);
                            File.Delete(phoneFilename);
                        }
                    }



                }
            }
        }

        public void Update(string id, Record rec)
        {
            Remove(id);
            Add(rec, id);
        }

        public void Backup()
        {
            ZipFile.CreateFromDirectory(rootdir, @".\backup.zip");
        }

        public void Restore()
        {
            Clear();
            ZipFile.ExtractToDirectory(@".\backup.zip", rootdir);
        }

        public void Dump()
        {

            List<string> records = new List<string>();
            string[] entries = Directory.GetFiles(rootdir + "\\entries");
            foreach (string entry in entries)
            {
                Record rec = GetByPath(entry);
                records.Add(rec.ToCSV());
            }

            using (StreamWriter outputFile = new StreamWriter(rootdir + "\\dump.csv"))
            {
                foreach (string line in records)
                    outputFile.WriteLine(line);
            }
        }

    }
}