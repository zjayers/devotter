using System;

namespace devotter.Models
{
    public class VersionInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        
        public VersionInfo(string version)
        {
            try
            {
                string[] parts = version.Split('.');
                if (parts.Length >= 3)
                {
                    Major = int.Parse(parts[0]);
                    Minor = int.Parse(parts[1]);
                    Patch = int.Parse(parts[2]);
                }
            }
            catch
            {
                Major = 1;
                Minor = 0;
                Patch = 0;
            }
        }
        
        public string IncrementMajor()
        {
            Major++;
            Minor = 0;
            Patch = 0;
            return ToString();
        }
        
        public string IncrementMinor()
        {
            Minor++;
            Patch = 0;
            return ToString();
        }
        
        public string IncrementPatch()
        {
            Patch++;
            return ToString();
        }
        
        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }
    }
}