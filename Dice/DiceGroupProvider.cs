using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using CodingSeb.ExpressionEvaluator;

namespace QuickDice.Dice
{
    public class DiceGroupProvider
    {
        protected const string REGEX = @"\d+[dD]\d+|[dD]\d+|\d+|[\+\-\/\*\^]";
        protected readonly char[] D = new[] {'d', 'D'};
        protected const char COMMA = ',';
        protected const char RANGE_DELIMITER = '-';
        protected const string GREATER_THAN_OR_EQUAL_WRONG = "=>";
        protected const string GREATER_THAN_OR_EQUAL_RIGHT = ">=";
        protected const string LESS_THAN_OR_EQUAL_WRONG = "=<";
        protected const string LESS_THAN_OR_EQUAL_RIGHT = "<=";
        protected const string EQUALS_WRONG = "=";
        protected const string EQUALS_RIGHT = "==";
        protected const string IN_RANGE = ":";
        protected readonly string[] COMPARATORS = new[] {GREATER_THAN_OR_EQUAL_RIGHT, LESS_THAN_OR_EQUAL_RIGHT, EQUALS_RIGHT};
        protected readonly string[] ALL_COMPARATORS = new[]
        {
            GREATER_THAN_OR_EQUAL_RIGHT, 
            LESS_THAN_OR_EQUAL_RIGHT, 
            EQUALS_RIGHT,
            GREATER_THAN_OR_EQUAL_WRONG,
            LESS_THAN_OR_EQUAL_WRONG,
            EQUALS_WRONG,
            "<",
            ">",
            ":"
        };

        public const string TOTAL = "total";
        public const string INDIVIDUAL = "individual";
        public const string SUCCESS = "success";
        public const string COUNTS_TWO = "countstwo";
        public const string SUBTRACTS = "subtracts";
        public const string EXPLOSIVE = "explosive";
        public const string ADD_TO_ALL = "addtoall";
        public const string DISPLAY_ORIGINAL = "original";

        protected RNGCryptoServiceProvider Roller;
        protected ExpressionEvaluator Eval;

        public DiceGroupProvider()
        {
            this.Roller = new RNGCryptoServiceProvider();
            this.Eval = new ExpressionEvaluator();
        }

        public IEnumerable<string> Parse(string diceString, string[] args)
        {
            string workingCopy = diceString;
            string[] splits = workingCopy.Split(COMMA);

            List<string> finalResults = new List<string>();

            bool total = args.Any(arg => arg.StartsWith(TOTAL, StringComparison.OrdinalIgnoreCase));
            
            string successString = "", countsTwoString = "", subtractsString = "", explosiveString = "";
            
            bool success = args.Any(arg => arg.StartsWith(SUCCESS, StringComparison.OrdinalIgnoreCase));
            if (success)
            {
                successString = args.First(arg => arg.StartsWith(SUCCESS, StringComparison.OrdinalIgnoreCase))
                    .Substring(SUCCESS.Length)
                    .Trim();

                if (successString.Length == 0)
                {
                    finalResults.Add("No Success parameters present.");
                    success = false;
                }
                
                successString = this.ReplaceProblems(successString);
                if(this.IsRange(successString) == false
                    && this.ALL_COMPARATORS.All(comp => successString.Contains(comp) == false))
                {
                    finalResults.Add("Success parameters does not include comparator. Inserting default.");
                    successString = successString.Insert(0, GREATER_THAN_OR_EQUAL_RIGHT);
                }
            }
            
            bool countsTwo = args.Any(arg => arg.StartsWith(COUNTS_TWO, StringComparison.OrdinalIgnoreCase));
            if (countsTwo)
            {
                countsTwoString = args.First(arg => arg.StartsWith(COUNTS_TWO, StringComparison.OrdinalIgnoreCase))
                    .Substring(COUNTS_TWO.Length)
                    .Trim();
                countsTwoString = this.ReplaceProblems(countsTwoString);
                if (this.IsRange(countsTwoString) == false 
                    && this.ALL_COMPARATORS.All(comp => countsTwoString.Contains(comp) == false))
                {
                    finalResults.Add("Counts for Two parameters does not include comparator. Inserting default.");
                    countsTwoString = countsTwoString.Insert(0, GREATER_THAN_OR_EQUAL_RIGHT);
                }
            }
            
            bool subtracts = args.Any(arg => arg.StartsWith(SUBTRACTS, StringComparison.OrdinalIgnoreCase));
            if (subtracts)
            {
                subtractsString = args.First(arg => arg.StartsWith(SUBTRACTS, StringComparison.OrdinalIgnoreCase))
                        .Substring(SUBTRACTS.Length)
                        .Trim();
                subtractsString = this.ReplaceProblems(subtractsString);
                if (this.IsRange(subtractsString) == false
                    && this.ALL_COMPARATORS.All(comp => subtractsString.Contains(comp) == false))
                {
                    finalResults.Add("Subtracts Successes parameters does not include comparator. Inserting default.");
                    subtractsString = subtractsString.Insert(0, LESS_THAN_OR_EQUAL_RIGHT);
                }
            }
            
            bool useExplosive = args.Any(arg => arg.StartsWith(EXPLOSIVE, StringComparison.OrdinalIgnoreCase));
            if (useExplosive)
            {
                explosiveString = args.First(arg => arg.StartsWith(EXPLOSIVE, StringComparison.OrdinalIgnoreCase))
                    .Substring(EXPLOSIVE.Length)
                    .Trim();
                explosiveString = this.ReplaceProblems(explosiveString);
                if (this.IsRange(explosiveString) == false 
                    && this.ALL_COMPARATORS.All(comp => explosiveString.Contains(comp) == false))
                {
                    finalResults.Add("Explosive parameters does not include comparator. Inserting default.");
                    explosiveString = explosiveString.Insert(0, GREATER_THAN_OR_EQUAL_RIGHT);
                }
            }

            bool addToAll = args.Any(arg => arg.StartsWith(ADD_TO_ALL, StringComparison.OrdinalIgnoreCase));

            bool original = args.Any(arg => arg.StartsWith(DISPLAY_ORIGINAL, StringComparison.OrdinalIgnoreCase));

            Tuple<bool, string>[] tupleArgs = new[]
            {
                new Tuple<bool, string>(success, successString),
                new Tuple<bool, string>(countsTwo, countsTwoString), 
                new Tuple<bool, string>(subtracts, subtractsString),
                new Tuple<bool, string>(useExplosive, explosiveString)
            };

            foreach (string split in splits)
            {
                int successes = 0;
                string splitCopy = split;
                if (this.ALL_COMPARATORS.Any(comp => splitCopy.Contains(comp)))
                {
                    string comparator = this.ALL_COMPARATORS.First(comp => splitCopy.Contains(comp));
                    success = true;
                    int index = splitCopy.IndexOf(comparator);
                    successString = splitCopy.Substring(index);
                    successString = this.ReplaceProblems(successString);
                    tupleArgs[0] = new Tuple<bool, string>(success, successString);
                    splitCopy = splitCopy.Remove(index);
                }

                List<int> firstGroup = new List<int>();
                List<int> results = new List<int>();
                List<string> resultStrings = new List<string>();
                MatchCollection group = System.Text.RegularExpressions.Regex.Matches(splitCopy, REGEX);
                for(int i = 0; i < group.Count; i++)
                {
                    string temp = group[i].Value;
                    int number = 1;
                    int faces = 2;
                    
                    List<int> localResults = new List<int>();
                    
                    if (this.IsDice(temp))
                    {
                        int dIndex = temp.IndexOfAny(this.D);
                        if (dIndex > 0)
                        {
                            number = int.Parse(temp.Substring(0, dIndex));
                        }
                        faces = int.Parse(temp.Substring(dIndex + 1));
                        
                        List<string> bits = new List<string>();

                        for (int j = 0; j < number; j++)
                        {
                            IEnumerable<int> moreResults = new List<int>();
                            int moreSuccesses = 0;
                            (moreResults, moreSuccesses) = this.Roll(faces, tupleArgs);
                            localResults.AddRange(moreResults);
                            if (!addToAll)
                            {
                                resultStrings.AddRange(moreResults.Select(result => result.ToString()));
                            }
                            if (!total)
                            {
                                successes += moreSuccesses;
                            }

                            int additionIndex = addToAll && firstGroup.Count > 0 ? firstGroup.Count : 1;
                            for (int add = 0; add < additionIndex; add++)
                            {
                                IEnumerator<int> enumerator = moreResults.GetEnumerator();
                                enumerator.MoveNext();
                                for (int k = 0; k < moreResults.Count(); k++)
                                {
                                    bits.Add(enumerator.Current.ToString());
                                    if (i != 0)
                                    {
                                        bits.Add(group[i - 1].Value);
                                    }
                                    else
                                    {
                                        bits.Add("+");
                                    }

                                    enumerator.MoveNext();
                                }
                            }
                            
                        }

                        bits.RemoveAt(bits.Count - 1);

                        int index = splitCopy.IndexOf(temp);
                        splitCopy = splitCopy.Remove(index, temp.Length)
                            .Insert(index, string.Join(" ", bits));

                        if (addToAll && i > 0)
                        {
                            for (int l = 0; l < firstGroup.Count; l++)
                            {
                                firstGroup[l] += localResults.Sum();
                            }
                        }
                        
                        results.AddRange(localResults);
                    }
                    else
                    {
                        int result = int.MinValue;
                        if (int.TryParse(temp, out result))
                        {
                            results.Add(result);
                            
                            if (addToAll && i > 0)
                            {
                                for (int l = 0; l < firstGroup.Count; l++)
                                {
                                    firstGroup[l] = this.Evaluate<int>(firstGroup[l] + group[i - 1].Value + result);
                                }
                            }

                            for (int l = 0; l < firstGroup.Count - 1; l++)
                            {
                                if (i == 0)
                                {
                                    splitCopy += "+" + result;
                                }
                                else
                                {
                                    splitCopy += group[i - 1].Value + result;
                                }
                            }
                        }
                    }

                    if (addToAll && i == 0)
                    {
                        firstGroup.AddRange(results);
                    }
                }
                
                int sum = this.Evaluate<int>(splitCopy);
                if (addToAll)
                {
                    for (int i = 0; i < firstGroup.Count; i++)
                    {
                        string add = original ? firstGroup[i] + " (" + results[i] + ")" : firstGroup[i].ToString();
                        resultStrings.Add(add);
                    }
                }

                if (success)
                {
                    if (total)
                    {
                        successes = 0;
                        
                        if ((this.IsRange(successString) && this.WithinRange(sum, successString))
                            || (this.IsRange(successString) == false && this.Evaluate<bool>(sum + successString)))
                        {
                            successes = 1;
                        }
                    }
                    else if (addToAll)
                    {
                        successes = 0;
                        foreach (int result in firstGroup)
                        {
                            if ((this.IsRange(successString) && this.WithinRange(result, successString))
                                || (this.IsRange(successString) == false && this.Evaluate<bool>(result + successString)))
                            {
                                successes += 1;
                            }
                        }
                    }

                    resultStrings.Add("Successes: " + successes);
                }
                
                resultStrings.Add("Total: " + sum);
                finalResults.AddRange(resultStrings);
            }

            return finalResults;
        }

        protected bool IsDice(string data)
        {
            return data.IndexOfAny(this.D) >= 0;
        }

        protected bool IsRange(string range)
        {
            return range.Contains(RANGE_DELIMITER);
        }

        protected bool WithinRange(int value, string range)
        {
            string[] split = range.Split(RANGE_DELIMITER);
            for (int i = 0; i < split.Length; i++)
            {
                split[i] = split[i].Trim();
            }

            int minimum = 0, maximum = 0;
            if (int.TryParse(split[0], out minimum) == false)
            {
                MessageBox.Show("Minimum range is not a valid number.");
                return false;
            }

            if (int.TryParse(split[1], out maximum) == false)
            {
                MessageBox.Show("Maximum range is not a valid number.");
                return false;
            }
            
            return (value - minimum) * (maximum - value) >= 0;
        }

        protected (IEnumerable<int>, int) Roll(int upper, params Tuple<bool, string>[] args)
        {
            byte[] bytes = new byte[4];
            this.Roller.GetBytes(bytes, 0, 4);
            int result = Math.Abs(BitConverter.ToInt32(bytes, 0) % upper) + 1;
            
            List<int> results = new List<int> { result };

            int successes = 0;

            //Successes
            if (args[0].Item1)
            {
                if ((this.IsRange(args[0].Item2) && this.WithinRange(result, args[0].Item2))
                    || (this.IsRange(args[0].Item2) == false && this.Evaluate<bool>(result + args[0].Item2)))
                {
                    successes += 1;
                }
                
                if (args.Length > 0)
                {
                    //counts for two
                    if (args[1].Item1 == true)
                    {
                        if ((this.IsRange(args[1].Item2) && this.WithinRange(result, args[1].Item2))
                            || (this.IsRange(args[1].Item2) == false && this.Evaluate<bool>(result + args[1].Item2)))
                        {
                            successes += 1;
                        }
                    }

                    //subtracts
                    if (args.Length > 1 && args[2].Item1 == true)
                    {
                        if ((this.IsRange(args[2].Item2) && this.WithinRange(result, args[2].Item2))
                            || (this.IsRange(args[2].Item2) == false && this.Evaluate<bool>(result + args[2].Item2)))
                        {
                            successes -= 1;
                        }
                    }

                    //explode
                    if (args.Length > 2 && args[3].Item1 == true)
                    {
                        if ((this.IsRange(args[3].Item2) && this.WithinRange(result, args[3].Item2))
                            || (this.IsRange(args[3].Item2) == false && this.Evaluate<bool>(result + args[3].Item2)))
                        {
                            IEnumerable<int> moreResults = new List<int>();
                            int moreSuccesses = 0;
                            (moreResults, moreSuccesses) = this.Roll(upper, args);
                            results.AddRange(moreResults);
                            successes += moreSuccesses;
                        }
                    }
                }
            }

            return (results, successes);
        }

        protected string ReplaceProblems(string formula)
        {
            string returnValue = formula;
            if (returnValue.Contains(GREATER_THAN_OR_EQUAL_WRONG))
            {
                returnValue = returnValue.Replace(GREATER_THAN_OR_EQUAL_WRONG, GREATER_THAN_OR_EQUAL_RIGHT);
            }
            else if (returnValue.Contains(LESS_THAN_OR_EQUAL_WRONG))
            {
                returnValue = returnValue.Replace(LESS_THAN_OR_EQUAL_WRONG, LESS_THAN_OR_EQUAL_RIGHT);
            }
            else if (returnValue.Contains(EQUALS_WRONG) 
                     && returnValue.Contains(EQUALS_RIGHT) == false
                     && returnValue.Contains(GREATER_THAN_OR_EQUAL_RIGHT) == false 
                     && returnValue.Contains(LESS_THAN_OR_EQUAL_RIGHT) == false)
            {
                returnValue = returnValue.Replace(EQUALS_WRONG, EQUALS_RIGHT);
            }
            else if (returnValue.Contains(IN_RANGE))
            {
                returnValue = returnValue.Replace(IN_RANGE, "");
            }

            return returnValue;
        }

        protected T Evaluate<T>(string value)
        {
            try
            {
                return this.Eval.Evaluate<T>(value);
            }
            catch (Exception e)
            {
                MessageBox.Show("Something went wrong evaluating a condition.");
                return default;
            }
        }
    }
}