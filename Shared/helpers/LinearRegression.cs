using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlgoTrader.Shared
{
    public static class SimpleLinearRegression
    {

        public static double Slope(List<double> x, List<double> y)
        {
            double slope = 0.0;
            if ((x != null) && (y != null) && (x.Count == y.Count) && (x.Count > 0))
            {
                slope = Correlation(x, y) / SumOfSquares(x);
            }
            return slope;
        }

        public static double Intercept(List<double> x, List<double> y)
        {
            double intercept = 0.0;
            if ((x != null) && (y != null) && (x.Count == y.Count) && (x.Count > 0))
            {
                double xave = Average(x);
                double yave = Average(y);
                intercept = yave - Slope(x, y) * xave;
            }
            return intercept;
        }

        public static double Average(List<double> values)
        {
            if ((values != null) && (values.Count > 0))
            {
                return values.Average();
            }
            return 0; ;
        }

        public static double SumOfSquares(List<double> values)
        {
            double sumOfSquares = 0.0;
            if ((values != null) && (values.Count > 0))
            {
                sumOfSquares = values.Sum();
                double average = Average(values);
                sumOfSquares -= average * average * values.Count;
            }
            return sumOfSquares;
        }

        public static double Correlation(List<double> x, List<double> y)
        {
            double correlation = 0.0;
            if ((x != null) && (y != null) && (x.Count == y.Count) && (x.Count > 0))
            {
                for (int i = 0; i < x.Count; ++i)
                {
                    correlation += x[i] * y[i];
                }
                double xave = Average(x);
                double yave = Average(y);
                correlation -= xave * yave * x.Count;
            }
            return correlation;
        }
    }
}
