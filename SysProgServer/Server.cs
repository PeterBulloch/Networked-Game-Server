using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using System.Windows.Forms;

namespace SysProgServer
{
    public partial class Server : Form
    {
        Room[] rooms = new Room[maxRooms];
        ConnectedUser[] users = new ConnectedUser[maxUsers];
        private Socket m_Socket;
        Socket tempSocket;
        private IPEndPoint m_LocalIPEndPoint;
        String[] words;
        System.Timers.Timer aTimer;
        System.Timers.Timer bTimer;
        const int maxUsers = 100;
        const int maxRooms = 20;
        const int maxPlayers = 5;
        const int roomIdle = 60;
        const int userIdle = 180;
        const int roundTime = 60;

        //Room contains an individual room's details
        struct Room
        {
            public string roomName;
            public int[] playerIndices;
            public int[] spectatorIndices;
            public int round;
            public int seconds;
            public string currentWord;
            public int idleTime;
            public bool inUse;
            public Color colour;
            public int size;
            public int players;
        }

        //contains any user connected to the server. Overlaps with players
        struct ConnectedUser
        {
            public string name;
            public Socket playerSocket;
            public bool hasCreatedRoom;
            public bool isConnected;
            public int idleTime;
            public int roomIndex;
            public int score;
            public bool drawer;
            public bool hasDrawn;
            public bool hasGuessed;
        }


        public Server()
        {
            InitializeComponent();
        }


        //on server load
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                words = File.ReadAllLines("Words.txt");
            }
            catch (FileNotFoundException) //already being read by a server. only happens through opening a new instance on the taskbar for some reason.
            {
                MessageBox.Show("Words file not found. Server must already exist on this PC. Closing.", "Error"); 
                Close();
            }
            for (int i = 0; i < maxUsers; i++)
            {
                if (i < maxRooms)
                {
                    rooms[i] = new Room();
                    rooms[i].playerIndices = new int[maxPlayers];
                    rooms[i].spectatorIndices = new int[maxUsers - maxPlayers];
                    for (int x = 0; x < maxUsers - maxPlayers; x++)
                    {
                        if (x < maxPlayers)
                        {
                            rooms[i].playerIndices[x] = -1;
                        }
                        rooms[i].spectatorIndices[x] = -1;
                    }
                }
                users[i] = new ConnectedUser();
            }
            try
            {
                // Create the socket, for TCP use
                m_Socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            }

            catch //(SocketException se)
            {
                // If an exception occurs, display an error message                	
                m_Socket.Shutdown(SocketShutdown.Both);
            }
            int iPort = 8009;
            try
            {

                // Create an Endpoint
                m_LocalIPEndPoint = new System.Net.IPEndPoint(IPAddress.Any, iPort);

                // Bind to the local IP Address and selected port
                m_Socket.Bind(m_LocalIPEndPoint);
            }

            catch
            {       // If an exception occurs, display an error message
                MessageBox.Show("Server already exists on this PC. Closing.", "Error");
                Close();
            }
            m_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            m_Socket.Blocking = false;
            aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(oneMS);
            aTimer.Interval = 1;
            aTimer.Enabled = true;
            aTimer.SynchronizingObject = this;
            bTimer = new System.Timers.Timer();
            bTimer.Elapsed += new ElapsedEventHandler(oneS);
            bTimer.Interval = 1000;
            bTimer.Enabled = true;
        }

        //Every one ms check each connected user's messages
        private void oneMS(object sender, ElapsedEventArgs e)
        {
            aTimer.Enabled = false;
            for (int i = 0; i < maxUsers; i++)
            {
                if (users[i].isConnected == true)
                {
                    try
                    {
                        byte[] ReceiveBuffer = new byte[1024];
                        int iReceiveByteCount;
                        iReceiveByteCount =
                        users[i].playerSocket.Receive(ReceiveBuffer, SocketFlags.None);
                        if (0 < iReceiveByteCount) //receives multiple msgs into same buffer / string.
                                                   //change method to handle this (delete / shift array based on the command
                        {
                            users[i].idleTime = userIdle;
                            String msg = Encoding.ASCII.GetString(ReceiveBuffer, 0,
                                                iReceiveByteCount);
                            List<String> msgs = msg.Split(',').ToList<String>();
                            for (int j = 0; j < msgs.Count; j++)
                            {
                                int room = users[i].roomIndex; ;
                                switch (msgs[j])
                                {

                                    case "b": //brush
                                        sendToRoom("" + msgs[j] + "," + msgs[j + 1] + ",", room);
                                        rooms[room].colour = Color.FromArgb(Int32.Parse(msgs[j + 1]));
                                        j++;
                                        break;
                                    case "c": //clear
                                        sendToRoom("" + msgs[j] + ",", room);
                                        break;
                                    case "s": //size
                                        sendToRoom("" + msgs[j] + "," + msgs[j + 1] + ",", room);
                                        rooms[room].size = Int32.Parse(msgs[j + 1]);
                                        j++;
                                        break;
                                    case "d": //draw
                                        sendToRoom("d," + msgs[j + 1] + "," + msgs[j + 2] + ",", room);
                                        j++;
                                        j++;
                                        break;
                                    case "ch": //chat
                                        if ((msgs[j + 1].Equals(rooms[room].currentWord)))
                                        {
                                            //if is nested not combined on purpose as to not hit the else statement
                                            if (!users[i].hasGuessed && !users[i].drawer)
                                            {
                                                sendToRoom("co," + rooms[room].seconds + "," + users[i].name + ",", room);
                                                users[i].score += rooms[room].seconds;
                                                users[i].hasGuessed = true;
                                                checkIfAllGuessed(room);
                                            }
                                        }
                                        else
                                        {
                                            sendToRoom(msgs[j] + "," + msgs[j + 1] + "," + msgs[j + 2] + ",", room);
                                        }
                                        j += 2;
                                        break;
                                    case "cr": //create
                                        if (!createRoom(msgs[j + 1]))
                                        {
                                            sendToUser("rf,", i);
                                        }
                                        j++;
                                        break;
                                    case "j": //join
                                        joinRoom(msgs[j + 1], i); //change
                                        j++;
                                        break;
                                    case "n": //name
                                        bool breaks = false;
                                        for (int l = 0; l < maxUsers; l++)
                                        {
                                            if (users[l].name != null && users[l].name.Equals(msgs[j + 1]))
                                            {
                                                sendToUser("nw,", i);
                                                breaks = true;
                                                break;
                                            }
                                        }
                                        if (breaks)
                                        {
                                            break;
                                        }
                                        users[i].name = msgs[j + 1];
                                        listBox1.Items.Add(msgs[j + 1]);
                                        sendToAll("ro," + getRooms());
                                        sendToUser("nr,", i);
                                        j++;
                                        break;
                                    case "lr": //leaveroom
                                        if (room == -1)
                                        {
                                            break;
                                        }
                                        users[i].roomIndex = -1;
                                        for (int x = 0; x < maxPlayers; x++)
                                        {
                                            if (rooms[room].playerIndices[x] == i)
                                            {
                                                --rooms[room].players;
                                                rooms[room].playerIndices[x] = -1;
                                                checkIfAllGuessed(room);
                                            }
                                        }
                                        for (int x = 0; x < maxUsers - maxPlayers; x++)
                                        {
                                            if(rooms[room].spectatorIndices[x] == i)
                                            {
                                                rooms[room].spectatorIndices[x] = -1;
                                            }
                                        }
                                        sendToRoom("rp," + users[i].name + " ,", room);
                                        sendToUser("ro," + getRooms(), i);
                                        break;
                                    case "sk": //skip
                                        rooms[room].seconds = 0;
                                        sendToRoom("co," + -20 + "," + users[i].name + ",", room);
                                        users[i].score -= 20;
                                        break;
                                    case "te": //test
                                        //txtServerLog.AppendText(msgs[j + 1]);
                                        j++;
                                        break;
                                    case "ls": //leaveserver
                                        users[i].isConnected = false;
                                        listBox1.Items.Remove(users[i].name);
                                        users[i].name = "";
                                        users[i].roomIndex = -1;
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception b)
                    {
                        Console.WriteLine(b.ToString());
                    }
                }
            }
            aTimer.Enabled = true;
        }

        public void checkIfAllGuessed(int room)
        {
            bool done = true;
            foreach (int p in rooms[room].playerIndices)
            {
                if (p >= 0 && p < maxUsers && !users[p].hasGuessed && !users[p].drawer)
                {
                    done = false;
                }
            }
            if (done &&rooms[room].players>1)
            {
                rooms[room].seconds = 0;
            }
        }






        //every one second, try to accept new connections
        private void oneS(object sender, ElapsedEventArgs e)
        {
            aTimer.Enabled = false;
            try
            {
                tempSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {

            }
            //sendToAll("te,");
            try
            {
                m_Socket.Listen(4);
            }
            catch
            {
            }
            bool maxReached = true;
            for (int i = 0; i < maxUsers; i++)
            {
                if (!users[i].isConnected)
                {
                    try
                    {
                        users[i].playerSocket = m_Socket.Accept();
                        sendToUser("mr," + maxRooms + ",", i);
                        maxReached = false;
                        users[i].playerSocket.Blocking = false;
                        users[i].idleTime = userIdle;
                        users[i].isConnected = true;
                        users[i].hasCreatedRoom = false;
                        users[i].name = "";
                        users[i].playerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                        users[i].roomIndex = -1;
                    }
                    catch //(SocketException se)
                    {

                    }
                }
            }
            if (maxReached)
            {
                try
                {
                    tempSocket = m_Socket.Accept();
                    String szData = "sf,";
                    int iBytesSent;
                    byte[] byData = Encoding.ASCII.GetBytes(szData);
                    iBytesSent = tempSocket.Send(byData, SocketFlags.None);
                    //tempSocket.Shutdown(SocketShutdown.Both);
                }
                catch
                {

                }
            }

            //decrease the seconds left in a room, and handle if it reaches 0
            for (int i = 0; i < maxRooms; i++)
            {
                if (rooms[i].inUse == true)
                {
                    if (rooms[i].seconds == 0)
                    {
                        secondsUp(i, rooms[i].round);
                    }
                    --rooms[i].seconds;
                    sendToRoom("t," + rooms[i].seconds + ",", i);
                }

                bool useCheck = false;
                foreach (int p in rooms[i].playerIndices)
                {
                    if (p >= 0 && p < maxUsers)
                    {
                        useCheck = true;
                        rooms[i].idleTime = roomIdle;
                    }
                }
                if (!useCheck)
                {
                    if (rooms[i].idleTime == 0)
                    {
                        rooms[i].inUse = false;
                        sendToAll("ro," + getRooms());
                        listBox2.Items.Remove(rooms[i].roomName);
                        foreach (int p in rooms[i].spectatorIndices)
                        {
                            if (p >= 0 && p < maxUsers)
                            {
                                sendToUser("rd,", p);
                            }
                        }
                    }

                    --rooms[i].idleTime;
                }
            }

            //decrease the value (lower=idle for longer) and handle if they are idle for too long
            for (int k = 0; k < maxUsers; k++)
            {
                if (users[k].isConnected)
                {
                    if (users[k].idleTime == 0)
                    {
                        sendToUser("ki,", k);
                        listBox1.Items.Remove(users[k].name);
                    }
                    --users[k].idleTime; //check for 0 and disconnect them
                }
            }
            aTimer.Enabled = true;
        }

        //handle there being no seconds left in a room
        private void secondsUp(int roomID, int round)
        {
            for (int i = 0; i < maxRooms; i++)
            {
                if (rooms[i].inUse == true && i == roomID)
                {
                    if (round == 0)
                    {
                        ++rooms[roomID].round;
                        sendToRoom("r," + rooms[roomID].round + ",", roomID);
                    }
                    startRound(i, round);
                }
            }
        }

        //starts a new round for a given room, or assigns a new drawer in a current round
        public void startRound(int roomID, int round)
        {
            foreach (int i in rooms[roomID].playerIndices)
            {
                if (i >= 0 && i < maxUsers)
                {
                    users[i].drawer = false; //reset who the current drawer is
                }
            }
            sendToRoom("c,", roomID);
            sendToRoom("s,7,", roomID);
            sendToRoom("b," + Color.Black.ToArgb() + ",",roomID);
            rooms[roomID].size = 7;
            rooms[roomID].colour = Color.Black;
            foreach (int i in rooms[roomID].playerIndices)
            {
                if (i >= 0 && i < maxUsers && !users[i].hasDrawn)
                {
                    //assign the new drawer inside the round
                    rooms[roomID].seconds = roundTime;
                    assignDrawer(roomID);
                    Random rnd = new Random();
                    string newWord = words[rnd.Next(words.Length)];
                    rooms[roomID].currentWord = newWord;
                    sendToRoom("w," + newWord + ",", roomID);
                    return;
                }
            }
            //reset who has drawn, for the new round
            foreach (int i in rooms[roomID].playerIndices)
            {
                if (i >= 0 && i < maxUsers)
                {
                    users[i].hasDrawn = false;
                }
            }
            rooms[roomID].round = ++round;
            sendToRoom("r," + rooms[roomID].round + ",", roomID);
            if (rooms[roomID].round == 4) //if round 3 has finished
            {
                //determine winner
                string winner = "";
                int score = Int32.MinValue;
                foreach (int i in rooms[roomID].playerIndices)
                {
                    if (i >= 0 && i < maxUsers)
                    {
                        if (users[i].score > score)
                        {
                            winner = users[i].name;
                            score = users[i].score;
                        }
                        users[i].score = 0;
                    }
                }
                //end the game / move to set up phase
                rooms[roomID].seconds = roundTime/4;
                rooms[roomID].round = 0;
                sendToRoom("r," + rooms[roomID].round + ",", roomID);
                sendToRoom("w,,", roomID);
                sendToRoom("gr,", roomID);
                sendToRoom("wr," + winner + "," + score + ",", roomID);
                return;
            }
            else
            {
                //start first drawer in a new round
                rooms[roomID].seconds = roundTime;
                assignDrawer(roomID);
                Random rnd = new Random();
                string newWord = words[rnd.Next(words.Length)];
                rooms[roomID].currentWord = newWord;
                sendToRoom("w," + newWord + ",", roomID);
            }
        }

        //assign a drawer to a room, and the rest of the players as guessers
        public void assignDrawer(int roomID)
        {
            bool unassigned = true;
            foreach (int i in rooms[roomID].playerIndices)
            {
                if (i >= 0 && i < maxUsers)
                {
                    users[i].hasGuessed = false;
                    if (!users[i].hasDrawn && unassigned)
                    {
                        sendToUser("dr,", i);
                        sendToRoom("dn," + users[i].name+",", roomID);
                        users[i].hasDrawn = true;
                        users[i].drawer = true;
                        unassigned = false;
                    }
                    else
                    {
                        sendToUser("gr,", i);
                    }
                }
            }
            sendToRoom("c,", roomID);
        }

        //send data to a specific user
        public void sendToUser(string data, int id)
        {
            String szData = data;
            int iBytesSent;
            byte[] byData = Encoding.ASCII.GetBytes(szData);
            try
            {
                iBytesSent = users[id].playerSocket.Send(byData, SocketFlags.None);
            }
            catch
            {

            }
        }

        //send data to all players in a room
        public void sendToRoom(string data, int roomIndex)
        {
            foreach (int i in rooms[roomIndex].playerIndices)
            {
                if (i >= 0 && i < maxUsers)
                {
                    sendToUser(data, i);
                }
            }
            foreach(int s in rooms[roomIndex].spectatorIndices)
            {
                if (s >= 0 && s < maxUsers)
                {
                    sendToUser(data, s);
                }
            }
        }

        //send to all currently connected users
        public void sendToAll(string data)
        {
            for (int i = 0; i < maxUsers; i++)
            {
                if (users[i].isConnected)
                {
                    sendToUser(data, i);
                }
            }
        }

        //creates a new basic room if there is room for one
        public bool createRoom(string roomName)
        {
            for (int i = 0; i < maxRooms; i++)
            {
                if (rooms[i].inUse != true && !rooms[i].Equals(roomName))
                {
                    rooms[i].roomName = roomName;
                    rooms[i].seconds = roundTime/4;
                    rooms[i].round = 0;
                    rooms[i].inUse = true;
                    rooms[i].idleTime = roomIdle;
                    rooms[i].colour = Color.Black;
                    rooms[i].size = 5;
                    rooms[i].players = 0;
                    sendToAll("ro," + getRooms());
                    listBox2.Items.Add(roomName);
                    return true;
                    //update room list to all users
                }
            }
            return false;
        }

        //return a string containing the current rooms in a format fit for the PDU
        public string getRooms()
        {
            string rooms = "";
            for (int i = 0; i < maxRooms; i++)
            {
                if (this.rooms[i].inUse)
                {
                    rooms += this.rooms[i].roomName + ",";
                }
                else
                {
                    rooms += ",";
                }
            }
            return rooms;
        }

        //join a user to a specific room if there is space
        public void joinRoom(string roomName, int id)
        {
            bool space = false;
            int i;
            int roomID = 0;
            for (i = 0; i < maxRooms; i++)
            {
                if (rooms[i].roomName!=null&&rooms[i].roomName.Equals(roomName)) 
                {
                    roomID = i;
                    if (rooms[i].players < maxPlayers)
                    {
                        space = true;
                        users[id].drawer = false;
                        users[id].score = 0;
                        users[id].hasDrawn = false;
                        users[id].hasGuessed = false;
                        users[id].roomIndex = i;
                        addToRoom(i, id);
                        sendToRoom("np," + users[id].name + "," + users[id].score + ",", i);
                        roomDetails(i, id);
                        //send confirmation & room details
                    }
                }


            }
            if (!space)
            {
                users[id].roomIndex = roomID;
                for (int s = 0; s < maxUsers-maxPlayers; s++)
                {
                    if (rooms[roomID].spectatorIndices[s] == -1)
                    {
                        rooms[roomID].spectatorIndices[s] = id;
                        users[id].idleTime = 600;
                        break;
                    }
                }
                //join as spectator
                sendToUser("ns,", id); //no space in the room
                roomDetails(roomID, id);
            }

        }

        public void addToRoom(int room, int user)
        {
            rooms[room].players++;
            for (int i = 0; i < maxPlayers; i++)
            {
                if (rooms[room].playerIndices[i] == -1)
                {
                    rooms[room].playerIndices[i] = user;
                    return;
                }
            }
        }

        //send details to a user who first joins a room
        private void roomDetails(int room, int id)
        {
            sendToUser("t," + rooms[room].seconds + ",", id);
            sendToUser("w," + rooms[room].currentWord + ",", id);
            sendToUser("b," + rooms[room].colour.ToArgb() + ",", id);
            sendToUser("s," + rooms[room].size + ",", id);
            sendToUser("r," + rooms[room].round + ",", id);
            foreach (int i in rooms[room].playerIndices)
            {
                if (i >= 0 && i < maxUsers && i != id)
                {
                    sendToUser("np," + users[i].name + "," + users[i].score + ",", id);
                    if (users[i].drawer)
                    {
                        sendToUser("dn," + users[i].name + ",", id);
                    }
                }
            }  
        }


        //on server close, send a kill message to all users and close all sockets
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            for (int i = 0; i < maxUsers; i++)
            {
                try
                {
                    if (users[i].isConnected)
                    {
                        sendToUser("k,", i);
                    }
                    users[i].playerSocket.Shutdown(SocketShutdown.Both);

                }
                catch
                {

                }
            }
            try
            {
                m_Socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {

            }
            try
            {
                tempSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {

            }
        }

    }
}
