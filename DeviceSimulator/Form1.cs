using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace DeviceSimulator
{
    public partial class DeviceSimulator : Form
    {
        public string ConnectionString { get; set; }

        int telemetryInterval = 1;
        private static DeviceClient deviceClient;
        bool Loop = true;

        public DeviceSimulator()
        {
            InitializeComponent();
            
            ConnectionString = txtConnectionString.Text;
            try
            {
                telemetryInterval = int.Parse(txtInterval.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Interval must be a interger");
            }
        }

        private void ConnectionChange(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            toolStripStatusLabel1.Text = status.ToString();

            if (status == ConnectionStatus.Connected)
            {
                btnStart.Invoke((MethodInvoker)delegate
                {
                    btnStart.Enabled = true;
                });

                btnSend.Invoke((MethodInvoker)delegate
                {
                    btnSend.Enabled = true;
                });
            }               
            else
            {
                btnStart.Invoke((MethodInvoker)delegate
                {
                    btnStart.Enabled = false;
                });

                btnSend.Invoke((MethodInvoker)delegate
                {
                    btnSend.Enabled = false;
                });
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            ConnectionString = txtConnectionString.Text;

            if (btnUpdate.Text == "Connect")
            {
                Connect();
            }
            else
                Disconnect();
        }
        private void Connect()
        {
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(ConnectionString, TransportType.Mqtt);
                deviceClient.SetConnectionStatusChangesHandler(ConnectionChange);
                deviceClient.SetMethodHandlerAsync("SetInterval", DirectMethodResponse, null);
                deviceClient.SetDesiredPropertyUpdateCallbackAsync(TwinResponse, null);
               
                btnUpdate.Text = "Disconnect";                
                statusStrip1.Refresh();
            }
            catch (Exception)
            {
                MessageBox.Show("Not able to connect. Please check your connection string and try again");
            }
        }
        private void Disconnect()
        {
            deviceClient.CloseAsync();
            btnUpdate.Text = "Connect";            
            statusStrip1.Refresh();
        }
        private Task TwinResponse(TwinCollection desiredProperties, object userContext)
        {
            string result = "{\"result\":\"Executed twin update\"}";
            
            txtStatus.Invoke((MethodInvoker)delegate {
                txtDesired.AppendText("Twin updaded: " + desiredProperties.ToJson() + Environment.NewLine);   
            });
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private Task<MethodResponse> DirectMethodResponse(MethodRequest methodRequest, object userContext)
        {
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            bool error = false;
            var json = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);
                       
            try
            {
                telemetryInterval = int.Parse(json);
            }
            catch (Exception)
            {
                error = true;
            }

            txtInterval.Invoke((MethodInvoker)delegate
            {
                txtInterval.Text = json;
            });

            txtStatus.Invoke((MethodInvoker)delegate {
                if(!error)
                    txtStatus.AppendText("Method called, interval updated"+ Environment.NewLine);
                else
                    txtStatus.AppendText("Method called but interval could not be updated" + Environment.NewLine);

            });
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private string GetPayload()
        {
            string results = "";
            Random rand = new Random();

            double r = 40 + rand.NextDouble() * 20;
            results = rtbPayload.Text.Replace("%%%", r.ToString());

            return results;
        }
        private async void SendReportedPropertiesAsync()
        {
            TwinCollection reportedProperties;
            reportedProperties = new TwinCollection();
            foreach (DataGridViewRow row in dgReported.Rows)
            {
                if (row.Cells[0].Value != null)
                {
                    string Name = row.Cells[0].Value.ToString();
                    string Value = row.Cells[1].Value.ToString();
                    reportedProperties[Name] = Value;
                }
            }
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }
        private async void SendDeviceToCloudMessagesAsync()
        {
            Random rand = new Random();

            while (Loop)
            {
                var messageString = JsonConvert.SerializeObject(GetPayload());
                var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageString));

                foreach (DataGridViewRow row in dgMessageProp.Rows)
                {
                    if (row.Cells[0].Value != null)
                    {
                        string Name = row.Cells[0].Value.ToString();
                        string Value = row.Cells[1].Value.ToString();
                        message.Properties.Add(Name, Value);
                    }
                }
                // Send the tlemetry message
                await deviceClient.SendEventAsync(message);
                txtStatus.AppendText(string.Format("{0} > Sending message: {1}", DateTime.Now, messageString) + Environment.NewLine);

                await Task.Delay(telemetryInterval * 1000);
            }
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            SendDeviceToCloudMessagesAsync();
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            if (Loop)
            {
                btnSend.Text = "Stop";
                Loop = true;
            }
            else
            {
                btnSend.Text = "Start";
                Loop = false;
            }
            SendReportedPropertiesAsync();
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }
    }
}   

