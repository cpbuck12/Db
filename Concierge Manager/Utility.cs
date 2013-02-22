namespace Concierge_Manager
{
    public static class Utility
    {
        public static string SpecialtyTrim(this string specialty)
        {
            specialty = specialty.Trim();
            while (specialty.EndsWith("#") && specialty != string.Empty)
                specialty = specialty.Substring(0, specialty.Length).Trim();
            return specialty;
        }
    }
}