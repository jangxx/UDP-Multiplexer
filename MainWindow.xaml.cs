using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace udp_mux
{
    public class AddressTuple
    {
        public string? address { get; set; }
        public UInt16? port { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<AddressTuple> inputAddresses = new ObservableCollection<AddressTuple>() { new AddressTuple() };
        private ObservableCollection<AddressTuple> outputAddresses = new ObservableCollection<AddressTuple>() { new AddressTuple() };

        public MainWindow()
        {
            InitializeComponent();

            icInputAddresses.ItemsSource = inputAddresses;
            icOutputAddresses.ItemsSource = outputAddresses;
        }

        private void Btn_addInput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_removeInput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_addOutput(object sender, RoutedEventArgs e)
        {
            outputAddresses.Add(new AddressTuple());
        }

        private void Btn_removeOutput(object sender, RoutedEventArgs e)
        {
            inputAddresses.Add(new AddressTuple());
        }

        private void Btn_start(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Inputs: " + String.Join(", ", inputAddresses.ToList().ConvertAll(new Converter<AddressTuple, String>(addr => addr.address + ":" + addr.port))));
        }

        private void Btn_saveConfig(object sender, RoutedEventArgs e)
        {
        }
    }
}
