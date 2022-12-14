using SoftwareArchitektur.ArchitekturSuggester.Models;
using SoftwareArchitektur.Utility.Models;

namespace SoftwareArchitektur.ArchitekturSuggester;

public class CircularDependencyChecker
{
    private readonly List<CircularDependencyCheckerModel> _dependentServices;

    private readonly List<string> _packageNameLookUp;

    private readonly List<CircularDependencyCheckerModel> _packageModels;


    public CircularDependencyChecker(List<ServiceModel> dependentServices)
    {
        _dependentServices = dependentServices.Select(s => new CircularDependencyCheckerModel(s)).ToList();
        _packageNameLookUp = dependentServices.Select(s => s.Name).ToList();
        _packageModels = new List<CircularDependencyCheckerModel>();
    }

    public List<PackageModel> CreatePackages()
    {
        while (_dependentServices.Any())
        {
            var origin = new CircularDependencyTrackingModel(GetServiceWithMostNumberOfCalls());
            Console.WriteLine("Checking Dependencies for Service" + origin.GetBaseModelName);
            var newPackage = CreatePackage(origin!);
             
            _packageModels.Add(newPackage.GetBaseModel);
             RemoveServicesContainedInPackage(newPackage.GetBaseModel);
        }

        return _packageModels.Select(cd => cd.ToPackageModel()).ToList();
    }

    private CircularDependencyCheckerModel? GetServiceWithMostNumberOfCalls()
    {
        var origin = _dependentServices.MaxBy(d => d.DependsOn.Sum(dr => dr.NumberOfCalls));
        return origin;
    }

    private CircularDependencyTrackingModel CreatePackage(CircularDependencyTrackingModel checkerModel)
    {
        while (checkerModel?.DependsOn?.Count > 0)
        {
            var dependency = checkerModel.DependsOn[0];
            checkerModel.DependsOn.RemoveAt(0);
            Console.WriteLine($"Check Dependency: {dependency.Callee},  {checkerModel.DependsOn.Count} Dependencies Left");

            var calledService = _dependentServices.FirstOrDefault(s => s.BaseServiceModel.Name == dependency.Callee);

            if (calledService == null)
            {
                if (_packageNameLookUp.Any(s => s == dependency.Callee))
                {
                    calledService = _packageModels.FirstOrDefault(c => c.Contains.Any(p => p.BaseServiceModel.Name == dependency.Callee));
                }
                else
                {
                    continue;
                }
            }

            checkerModel.AddToVisited(calledService);

            if (checkerModel.HasDuplicate)
            {
                var duplicateSlice = checkerModel.GetDuplicateSlice();

                foreach (var dependencyCheckerModel in duplicateSlice)
                {
                    checkerModel.GetBaseModel.EatDifferentModels(dependencyCheckerModel);
                    checkerModel.Visited.RemoveAt(checkerModel.Visited.Count-1);
                }
            }
            else
            {
                CreatePackage(checkerModel);
            }
        }

        if (checkerModel.Visited.Count > 1)
        {
            checkerModel.Visited.RemoveAt(checkerModel.Visited.Count-1);
        }
    

        return checkerModel;
    }


    //
    //    
    // }
    // private void ProcessDependency(CircularDependencyCheckerModel checkerModel, CircularDependencyCheckerModel? calledService,
    //     CircularDependencyRelationModel dependency)
    // {
    //     if (calledService == null)
    //     {
    //         ProcessDependencyInDifferentPackage(checkerModel, dependency);
    //     }
    //     else if (checkerModel.Visited.Any(d => dependency.Callee == d.BaseServiceModel.Name))
    //     {
    //         checkerModel.EatDifferentModels(calledService);
    //     }
    //     else
    //     {
    //         AddToVisited(checkerModel, calledService);
    //     }
    // }
    // private void ProcessDependencyInDifferentPackage(CircularDependencyCheckerModel checkerModel,
    //     CircularDependencyRelationModel dependency)
    // {
    //     if (CheckIfDependencyIsAlreadyAdded(dependency) &&
    //         !CheckIfDependencyIsAlreadyInternal(checkerModel, dependency))
    //     {
    //         ConsumePackageWithCircularDependency(checkerModel, dependency);
    //     }
    // }
    private void RemoveServicesContainedInPackage(CircularDependencyCheckerModel newPackage)
    {
        foreach (var service in newPackage.Contains)
        {
            _dependentServices.Remove(_dependentServices.FirstOrDefault(s =>
                s.BaseServiceModel.Name == service.BaseServiceModel.Name));
        }
    }


    private void ConsumePackageWithCircularDependency(CircularDependencyCheckerModel checkerModel,
        CircularDependencyRelationModel dependency)
    {
        var packageToEat =
            _packageModels.First(p => p.Contains.Any(s => s.BaseServiceModel.Name == dependency.Callee));
        Console.WriteLine(
            $"Possible Circular Dependency For Package {checkerModel.PackageName} With Package {packageToEat.PackageName}, From Service {dependency.Caller} To {dependency.Callee}");

        checkerModel.EatDifferentModels(packageToEat);
        _packageModels.Remove(packageToEat);
    }

    private static bool CheckIfDependencyIsAlreadyInternal(CircularDependencyCheckerModel checkerModel,
        CircularDependencyRelationModel dependency)
    {
        return checkerModel.Contains.Any(s => s.BaseServiceModel.Name == dependency.Callee);
    }

    private bool CheckIfDependencyIsAlreadyAdded(CircularDependencyRelationModel dependency)
    {
        return _packageNameLookUp.FirstOrDefault(s => s == dependency.Callee) != null;
    }

    internal class CircularDependencyCheckerModel : IEquatable<CircularDependencyCheckerModel>
    {
        public string PackageName { get; set; } = string.Empty;

        public List<CircularDependencyCheckerModel> Contains => GetAllContained();

        public readonly List<CircularDependencyCheckerModel> Eaten = new List<CircularDependencyCheckerModel>();

        private static int _counter = 0;

        public ServiceModel BaseServiceModel { get; private set; }

        //todo Create Limit on minimum number of calls that it has to think about

        public readonly List<CircularDependencyRelationModel> DependsOn = new List<CircularDependencyRelationModel>();

        public bool Equals(CircularDependencyCheckerModel? other)
        {
            if (other == null)
            {
                return false;
            }

            return BaseServiceModel.Name == other.BaseServiceModel.Name;
        }

        private List<CircularDependencyCheckerModel> GetAllContained()
        {
            var newList = new List<CircularDependencyCheckerModel>();
            newList.AddRange(Eaten);
            newList.Add(this);
            return newList;
        }

        public CircularDependencyCheckerModel(ServiceModel model)
        {
            BaseServiceModel = model;
            _counter++;
            PackageName = $"Package{_counter}";
            DependsOn.AddRange(model.DependsOn.Select(d => new CircularDependencyRelationModel(d)).ToList());
        }

        public PackageModel ToPackageModel()
        {
            var packageModel = new PackageModel(PackageName);
            foreach (var eatenService in Contains)
            {
                packageModel.AddService(eatenService.BaseServiceModel);
            }

            return packageModel;
        }

        public void EatDifferentModels(CircularDependencyCheckerModel eatenCheckerModel)
        {
            if (BaseServiceModel == eatenCheckerModel.BaseServiceModel)
            {
                return;
            }

            eatenCheckerModel.Eaten.ForEach(v => v.PackageName = this.PackageName);

            ConsumeModelAndVisited(eatenCheckerModel);

            DigestDependencies(eatenCheckerModel);

            ClearDataInEatenModel(eatenCheckerModel);
        }

        private static void ClearDataInEatenModel(CircularDependencyCheckerModel eatenCheckerModel)
        {
            eatenCheckerModel.Eaten.Clear();
            eatenCheckerModel.DependsOn.Clear();
        }

        private void ConsumeModelAndVisited(CircularDependencyCheckerModel eatenCheckerModel)
        {
            Eaten.AddRange(eatenCheckerModel.Contains.Except(Contains));
            Eaten.Remove(this);
        }

        private void DigestDependencies(CircularDependencyCheckerModel eatenCheckerModel)
        {
            var newDependencies = eatenCheckerModel.DependsOn;
            DependsOn.Where(d => !Contains.Any(m => m.PackageName == d.Callee)).UnionBy(newDependencies, d => d.Callee);
            DependsOn.DistinctBy(d => d.Callee);
        }
    }

    internal class CircularDependencyRelationModel
    {
        public readonly string Caller;
        public readonly string Callee;
        public readonly long NumberOfCalls;

        public CircularDependencyRelationModel(DependencyRelationModel dependencyRelationModel)
        {
            Callee = dependencyRelationModel.Callee;
            Caller = dependencyRelationModel.Caller;
            NumberOfCalls = dependencyRelationModel.NumberOfCalls;
        }
    }

    internal class CircularDependencyTrackingModel
    {
        private CircularDependencyCheckerModel BaseModel { get; set; }

        public string GetBaseModelName => BaseModel.PackageName;

        public CircularDependencyCheckerModel GetBaseModel => BaseModel;

        public List<CircularDependencyRelationModel> DependsOn => Visited.LastOrDefault()?.DependsOn;

        public bool HasDuplicate => Visited.Count != Visited.Distinct().Count();

        public readonly List<CircularDependencyCheckerModel> Visited = new();

        public CircularDependencyTrackingModel(CircularDependencyCheckerModel baseModel)
        {
            BaseModel = baseModel;
            Visited.Add(baseModel);
        }

        public void AddToVisited(CircularDependencyCheckerModel visitedModel)
        {
            Visited.Add(visitedModel);
            DigestDependencies(visitedModel);
        }

        private void DigestDependencies(CircularDependencyCheckerModel eatenCheckerModel)
        {
            var newDependencies = Visited.SelectMany(d => d.DependsOn)
                .UnionBy(eatenCheckerModel.Contains.SelectMany(d => d.DependsOn), d => d.Callee);
            BaseModel.DependsOn.UnionBy(newDependencies, d => d.Callee);
            BaseModel.DependsOn.DistinctBy(d => d.Callee);
        }

        public List<CircularDependencyCheckerModel> GetDuplicateSlice()
        {
            var duplicates = Visited
                .GroupBy(s => s.PackageName)
                .OrderByDescending(g => g.Count())
                .First();

            var startIndex = Visited.FindIndex(d => duplicates.First().PackageName == d.PackageName);
            var lastIndex = Visited.FindLastIndex(d => duplicates.Last().PackageName == d.PackageName);
            return Visited.GetRange(startIndex, lastIndex-startIndex);
        }
    }
}