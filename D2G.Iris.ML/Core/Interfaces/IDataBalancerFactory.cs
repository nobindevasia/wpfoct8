using D2G.Iris.ML.Core.Enums;

namespace D2G.Iris.ML.Core.Interfaces
{
    public interface IDataBalancerFactory
    {
        IDataBalancer CreateBalancer(DataBalanceMethod method);
    }
}
