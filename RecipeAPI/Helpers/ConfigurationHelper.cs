namespace RecipeAPI.Helpers
{
    public class ConfigurationHelper
    {
        private static ConfigurationHelper _instance;

        public string RecipeFilePathValue { get; set; }

        private ConfigurationHelper()
        {
        }

        public static ConfigurationHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ConfigurationHelper();

                return _instance;
            }
        }

        public void DoSingletonOperation()
        {
            Console.WriteLine("singleton operation");
        }
    }

    public class GlobalConfig
    {
        public string RecipeFilePathValue { get; set; }
    }
}
