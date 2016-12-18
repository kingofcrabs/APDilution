﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace APDilution
{
    struct FactorInfo
    {
        public int rootVal;
        public int currentVal;
        public List<int> factors;
        public FactorInfo(int rootVal)
        {
            this.rootVal = rootVal;
            currentVal = rootVal;
            factors = new List<int>();
        }

        public override bool Equals(object obj)
        {
            if (factors.Count == 0)
                return base.Equals(obj);
            else
                return this.factors.Sum() == ((FactorInfo)obj).factors.Sum();
        }


        public FactorInfo(FactorInfo factorInfo)
        {
            // TODO: Complete member initialization
            rootVal = factorInfo.rootVal;
            currentVal = factorInfo.currentVal;
            factors = new List<int>();
            foreach (var factor in factorInfo.factors)
                factors.Add(factor);
        }
    }
    class FactorFinder
    {

        public List<int> GetBestFactors(int dilutionTimes)
        {
            if (dilutionTimes == 1)
                return new List<int>() { 1 };
            if(Configurations.Instance.IsGradualPipetting)
                return GetGradualFactors(dilutionTimes);

         
            //400's factors
            List<int> possibleFactors = new List<int>()
            {
               2,5,10,20,25,40,50
            };
            FactorFinder finder = new FactorFinder();
            FactorInfo rootFactor = new FactorInfo(dilutionTimes);
            List<FactorInfo> childrenFactor = new List<FactorInfo>();
            List<FactorInfo> finalFactors = new List<FactorInfo>();
            childrenFactor = finder.GetFactors(rootFactor, possibleFactors, finalFactors);
            List<FactorInfo> thisStepFactorInfos = new List<FactorInfo>();
            for (int i = 1; i < 4; i++) //4 steps
            {
                thisStepFactorInfos = new List<FactorInfo>();
                RemoveDuplicated(finalFactors);
                if (finalFactors.Count != 0)
                    break;
                foreach (var childFactor in childrenFactor)
                {
                    thisStepFactorInfos.AddRange(finder.GetFactors(childFactor, possibleFactors, finalFactors));
                }

                childrenFactor = RemoveDuplicated(thisStepFactorInfos);
                if (thisStepFactorInfos.Count == 0)
                    break;
            }
            finalFactors.AddRange(thisStepFactorInfos); //add last step;
            if (finalFactors.Count == 0)
                throw new Exception("Cannot find factors for dilution times: " + dilutionTimes.ToString());
            int minSum = finalFactors.Min(x => x.factors.Sum());
            var best = finalFactors.Where(x => x.factors.Sum() == minSum).First();
            return best.factors;
        }

        public List<int> GetGradualFactors(int dilutionTimes)
        {
            int gradualTimes = Configurations.Instance.GradualTimes;
            int needWellCnt = (int)Math.Log(dilutionTimes, gradualTimes);
            if (Math.Pow(gradualTimes, needWellCnt) != dilutionTimes)
                throw new Exception(string.Format("Invalid dilution times:{0}", dilutionTimes));
            List<int> factors = new List<int>();
            for (int i = 0; i < needWellCnt; i++)
                factors.Add(gradualTimes);
            return factors;
        }

        private static List<FactorInfo> RemoveDuplicated(List<FactorInfo> factorInfos)
        {
            HashSet<FactorInfo> distinctFactors = new HashSet<FactorInfo>(factorInfos);
            factorInfos.Clear();
            foreach (var factorInfo in distinctFactors)
                factorInfos.Add(factorInfo);
            return factorInfos;
        }

        public List<FactorInfo> GetFactors(FactorInfo factorInfo, List<int> possibleFactors, List<FactorInfo> finalFactors)
        {
            List<FactorInfo> childrenFactorInfos = new List<FactorInfo>();
            foreach (var possibleFactor in possibleFactors)
            {
                if (factorInfo.currentVal % possibleFactor == 0)
                {
                    FactorInfo childInfo = new FactorInfo(factorInfo);
                    childInfo.factors.Add(possibleFactor);
                    childInfo.currentVal /= possibleFactor;
                    if (childInfo.currentVal <= 50 )
                    {
                        if(childInfo.currentVal == 1)
                        {
                            finalFactors.Add(childInfo);
                        }
                        else if (possibleFactors.Contains(childInfo.currentVal))
                        {
                            childInfo.factors.Add(childInfo.currentVal);
                            finalFactors.Add(childInfo);
                        }

                    }
                    else
                    {

                        childrenFactorInfos.Add(childInfo);
                    }

                }
            }
            return childrenFactorInfos;

        }

        public List<int> GetValidDilutionTimes()
        {
            List<int> possibleFactors = new List<int>()
            {
               1,2,5,10,20,25,40,50
            };
            HashSet<int> vals = new HashSet<int>() { 2, 5, 10, 20, 25, 40, 50 };
            for (int i = 1; i < 4; i++)
            {
                vals = GetCombination(vals, possibleFactors);
            }
            vals.Add(1);
            var orderedVals = vals.OrderBy(x => x).ToList();
            return orderedVals; 
        }
     

        public HashSet<int> GetCombination(HashSet<int> vals, List<int> factors)
        {
            HashSet<int> newVals = new HashSet<int>();
            foreach (var factor in factors)
            {
                foreach (var val in vals)
                {
                    newVals.Add(val * factor);
                }
            }
            return newVals;
        }
    }
}