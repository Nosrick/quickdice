using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Z.Expressions;

namespace QuickDice.Dice
{
    public class DiceGroupProvider
    {
        protected readonly string Regex = @"\d+[dD]\d+|\d+|[+-/*^]";
        protected readonly char[] D = new[] {'d', 'D'};
        protected readonly char Comma = ',';

        public const string TOTAL = "total";
        public const string INDIVIDUAL = "individual";
        public const string SUCCESS = "success";
        public const string COUNTS_TWO = "countstwo";
        public const string SUBTRACTS = "subtracts";
        public const string EXPLOSIVE = "explosive";
        public const string ADD_TO_ALL = "addtoall";

        protected RNGCryptoServiceProvider Roller;

        public DiceGroupProvider()
        {
            this.Roller = new RNGCryptoServiceProvider();
        }

        public IEnumerable<string> Parse(string diceString, string[] args)
        {
            string original = diceString.Replace(" ", "");
            string workingCopy = original;
            string[] splits = workingCopy.Split(this.Comma);

            List<string> finalResults = new List<string>();

            bool total = args.Any(arg => arg.StartsWith(TOTAL, StringComparison.OrdinalIgnoreCase));
            bool countsTwo = args.Any(arg => arg.StartsWith(COUNTS_TWO, StringComparison.OrdinalIgnoreCase));
            int countsTwoRange = 0;
            if (countsTwo)
            {
                countsTwoRange = int.Parse(args.First(arg => arg.StartsWith(COUNTS_TWO, StringComparison.OrdinalIgnoreCase))
                    .Substring(COUNTS_TWO.Length)
                    .Trim());
            }
            bool subtracts = args.Any(arg => arg.StartsWith(SUBTRACTS, StringComparison.OrdinalIgnoreCase));
            int subtractsRange = 0;
            if (subtracts)
            {
                subtractsRange = int.Parse(args
                    .First(arg => arg.StartsWith(SUBTRACTS, StringComparison.OrdinalIgnoreCase))
                    .Substring(SUBTRACTS.Length)
                    .Trim());
            }
            
            bool useExplosive = args.Any(arg => arg.StartsWith(EXPLOSIVE, StringComparison.OrdinalIgnoreCase));
            int explosiveRange = 0;
            if (useExplosive)
            {
                explosiveRange = int.Parse(args
                    .First(arg => arg.StartsWith(EXPLOSIVE, StringComparison.OrdinalIgnoreCase))
                    .Substring(EXPLOSIVE.Length)
                    .Trim());
            }

            bool addToAll = args.Any(arg => arg.StartsWith(ADD_TO_ALL, StringComparison.OrdinalIgnoreCase));

            Tuple<bool, int>[] tupleArgs = new[]
            {
                new Tuple<bool, int>(countsTwo, countsTwoRange), 
                new Tuple<bool, int>(subtracts, subtractsRange),
                new Tuple<bool, int>(useExplosive, explosiveRange)
            };

            int successThreshold = int.MinValue;

            if (args.Any(arg => arg.StartsWith(SUCCESS, StringComparison.OrdinalIgnoreCase)))
            {
                string successString = args.First(arg => arg.StartsWith(SUCCESS))
                    .Substring(SUCCESS.Length)
                    .Trim();
                if (successString.Length > 0)
                {
                    successThreshold = int.Parse(successString);
                }
            }

            foreach (string split in splits)
            {
                int successes = 0;
                string splitCopy = split;
                List<int> results = new List<int>();
                List<string> resultStrings = new List<string>();
                MatchCollection group = System.Text.RegularExpressions.Regex.Matches(splitCopy, this.Regex);
                foreach (Match match in group)
                {
                    string temp = match.Value;
                    int number = 1;
                    int faces = 2;
                    
                    if (this.IsDice(temp))
                    {
                        int dIndex = temp.IndexOfAny(this.D);
                        if (dIndex > 0)
                        {
                            number = int.Parse(temp.Substring(0, dIndex));
                        }
                        faces = int.Parse(temp.Substring(dIndex + 1));
                        
                        List<string> bits = new List<string>();

                        for (int i = 0; i < number; i++)
                        {
                            IEnumerable<int> moreResults = new List<int>();
                            int moreSuccesses = 0;
                            (moreResults, moreSuccesses) = this.Roll(faces, successThreshold, tupleArgs);
                            results.AddRange(moreResults);
                            resultStrings.AddRange(moreResults.Select(result => result.ToString()));
                            if (!total)
                            {
                                successes += moreSuccesses;
                            }

                            IEnumerator<int> enumerator = moreResults.GetEnumerator();
                            enumerator.MoveNext();
                            for (int j = 0; j < moreResults.Count(); j++)
                            {
                                bits.Add(enumerator.Current.ToString());
                                bits.Add("+");

                                enumerator.MoveNext();
                            }
                        }
                        
                        bits.RemoveAt(bits.Count - 1);

                        int index = splitCopy.IndexOf(temp);
                        splitCopy = splitCopy.Remove(index, temp.Length)
                            .Insert(index, string.Join(" ", bits));
                    }
                    else
                    {
                        int result = int.MinValue;
                        if (int.TryParse(temp, out result))
                        {
                            results.Add(result);
                        }
                    }
                }

                if (successThreshold != int.MinValue)
                {
                    if (total)
                    {
                        successes = 0;
                        if (results.Sum(result => result) >= successThreshold)
                        {
                            successes = 1;
                        }
                    }

                    resultStrings.Add("Successes: " + successes);
                }
                
                resultStrings.Add("Total: " + Eval.Execute<int>(splitCopy).ToString());
                finalResults.AddRange(resultStrings);
            }

            return finalResults;
        }

        protected bool IsDice(string data)
        {
            return data.IndexOfAny(this.D) >= 0;
        }

        protected (IEnumerable<int>, int) Roll(int upper, int successThreshold = int.MinValue, params Tuple<bool, int>[] args)
        {
            byte[] bytes = new byte[4];
            this.Roller.GetBytes(bytes, 0, 4);
            int result = Math.Abs(BitConverter.ToInt32(bytes, 0) % upper) + 1;
            
            List<int> results = new List<int> { result };

            int successes = 0;

            if (successThreshold != int.MinValue)
            {
                if (result >= successThreshold)
                {
                    successes += 1;
                }
                
                if (args.Length > 0)
                {
                    //counts for two
                    if (args[0].Item1 == true)
                    {
                        if (result >= args[0].Item2)
                        {
                            successes += 1;
                        }
                    }

                    //subtracts
                    if (args.Length > 1 && args[1].Item1 == true)
                    {
                        if (result <= args[1].Item2)
                        {
                            successes -= 1;
                        }
                    }

                    //explode
                    if (args.Length > 2 && args[2].Item1 == true)
                    {
                        if (result >= args[2].Item2)
                        {
                            IEnumerable<int> moreResults = new List<int>();
                            int moreSuccesses = 0;
                            (moreResults, moreSuccesses) = this.Roll(upper, successThreshold, args);
                            results.AddRange(moreResults);
                            successes += moreSuccesses;
                        }
                    }
                }
            }

            return (results, successes);
        }
    }
}