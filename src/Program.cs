using Ecowitt;
using System.Text;

string ReadJsonFromFile(string fName)
{
    string s;
    using (StreamReader reader = new StreamReader(fName, Encoding.UTF8))
    {
        s = reader.ReadToEnd();
        reader.Close();
    }
    return s;
}

// See https://aka.ms/new-console-template for more information

var s = ReadJsonFromFile("historical_data.json");
var x = new EcowittInputData(s);
Thread.Sleep(2000);