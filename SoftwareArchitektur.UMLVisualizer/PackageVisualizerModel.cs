using SoftwareArchitektur.Utility.Models;

namespace SoftwareArchitektur.UMLVisualizer;

public class PackageVisualizerModel
{
    public PackageVisualizerModel(string packageName)
    {
        PackageName = packageName;
    }

    public string PackageName { get; set; }
    
    public List<string> HasService = new List<string>();


    //Sourcehttps://socratic.org/statistics/random-variables/addition-rules-for-variances

    public List<string> DependsOn = new List<string>();
}