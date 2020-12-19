using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickDice.Dice;

namespace QuickDice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        protected DiceGroupProvider DiceGroupProvider { get; set; }
        
        public MainWindow()
        {
            InitializeComponent();

            RollButton.Click += Roll;
            SuccessesCheckBox.Checked += SuccessesCheckBoxChanged;
            SuccessesCheckBox.Unchecked += SuccessesCheckBoxChanged;
            DiceBox.MouseDoubleClick += this.TextBoxClicked;
            SuccessParameters.MouseDoubleClick += this.TextBoxClicked;
            
            DiceGroupProvider = new DiceGroupProvider();
        }

        protected void TextBoxClicked(object sender, RoutedEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                textBox.Clear();
            }
        }

        protected void SuccessesCheckBoxChanged(object sender, RoutedEventArgs args)
        {
            if (SuccessesCheckBox.IsChecked.Value)
            {
                SuccessPanel.Visibility = Visibility.Visible;
            }
            else
            {
                SuccessPanel.Visibility = Visibility.Collapsed;
            }
        }

        protected void Roll(object sender, RoutedEventArgs args)
        {
            this.ResultsList.Items.Clear();
            
            List<string> options = new List<string>();

            if (this.SuccessesCheckBox.IsChecked.Value)
            {
                if (this.IndividualRadio.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.INDIVIDUAL);
                }
                else if (this.TotalRadio.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.TOTAL);
                }

                if (this.SuccessesCheckBox.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.SUCCESS + this.SuccessParameters.Text);
                }
                
                if (this.ExplosiveCheckBox.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.EXPLOSIVE + this.ExplosiveRangeText.Text);
                }

                if (this.RangeCountsForTwoCheckBox.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.COUNTS_TWO + this.TwoCountRangeText.Text);
                }

                if (this.RangeSubtractCheckBox.IsChecked.Value)
                {
                    options.Add(DiceGroupProvider.SUBTRACTS + this.RangeSubtractsText.Text);
                }
            }

            if (this.AddBonusIndividuallyCheckBox.IsChecked.Value)
            {
                options.Add(DiceGroupProvider.ADD_TO_ALL);
            }

            IEnumerable<string> results = this.DiceGroupProvider.Parse(this.DiceBox.Text, options.ToArray());

            foreach (string result in results)
            {
                this.ResultsList.Items.Add(result);
            }
        }
    }
}