namespace RecipeApp.Models
{
    public class Step
    {
        public int Num { get; set; }

        public string Instructions { get; set; }

        public string? ImageData { get; set; }

        public string StepNumberDisplay
        {
            get
            {
                return string.Format("Step {0}", (Num + 1).ToString());
            }
        }
    }
}
