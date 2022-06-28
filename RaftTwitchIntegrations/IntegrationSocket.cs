using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static TwitchItegration;
using Debug = UnityEngine.Debug;

/*
 * Code referenced from Blargerist. Thanks blarg!
 */

internal class IntegationSocket
{
    private bool starting;
    private bool running;
    private bool connected;
    private bool shouldShutdown = false;
    public string id;
    private int port;
    private readonly object lock_socket = new object();
    private Socket underlying_socket;
    private Socket Socket
    {
        get
        {
            lock (lock_socket)
            {
                return underlying_socket;
            }
        }
        set
        {
            lock (lock_socket)
            {
                underlying_socket = value;
            }
        }
    }

    public IntegationSocket()
    {

    }

    public static IntegationSocket Start(string id, int port)
    {
        IntegationSocket socket = new IntegationSocket
        {
            starting = true,
            id = id,
            port = port
        };

        Task.Factory.StartNew(socket.Run, id);
        Debug.Log("Queueing socket.... ");
        socket.starting = false;
        Thread.Sleep(10000);
        return socket;
    }

    protected void Run(object state)
    {
        Debug.Log("Starting socket connection....");
        running = true;
        //Keep making new sockets
        while (!shouldShutdown)
        {
            try
            {
                // Data buffer for incoming data.  
                byte[] bytes = new byte[1024];
                // Establish the remote endpoint for the socket.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, this.port);

                using (Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        Socket.Connect(remoteEP);
                        connected = true;
                        Debug.Log("Socket connected");

                        //Send the name of the integration we want to listen to
                        byte[] toSend = Encoding.UTF8.GetBytes("{\"type\":\"INIT\", \"id\":\"62b40fb61b56981a68c4c6ec\"}");
                        Socket.Send(toSend);

                        int index = 0;

                        while (!shouldShutdown && Socket.Connected)
                        {
                            Debug.Log("Reading....");
                            index += Socket.Receive(bytes, index, bytes.Length - index, SocketFlags.None);
                            Debug.Log($"Parsing.....");

                            try
                            {
                                string msg = Encoding.UTF8.GetString(bytes, 0, index);

                                Debug.Log(msg);
                                index = 0;
                                var jobj = JObject.Parse(msg);
                                var data = (JObject)jobj["data"];
                                var delay = (int)(data["values"]["delay"] ?? 0);

                                RewardData rewardData = new RewardData((string)data["type"], data, delay, DateTime.UtcNow);
                                rewardsQueue.Enqueue(rewardData);
                            }
                            catch (Exception e)
                            {
                                Debug.Log($"Exception parsing msg: {e}");
                            }
                        }
                    }
                    catch (SocketException e)
                    {
                        //Causes tons of log spam.
                        Debug.Log($"Unexpected exception : {e}");
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"Unexpected exception : {e}");
                    }
                }
                if (connected)
                {
                    Debug.Log("Socket disconnected");
                }
                connected = false;
            }
            catch (Exception e)
            {
                //Ignore
                Debug.Log($"{e}");
            }
            if (!shouldShutdown)
            {
                Thread.Sleep(5000);
            }
        }
        connected = false;
        running = false;
    }

    //Tells the connection to shut down
    public void Shutdown()
    {
        shouldShutdown = true;

        // Release the socket.  
        try
        {
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception e)
        {
            Debug.Log($"{e}");
        }
        try
        {
            Socket.Close();
        }
        catch (Exception e)
        {
            Debug.Log($"{e}");
        }

        while (starting || connected || running)
        {
            Debug.Log("5");
        }
    }
}
