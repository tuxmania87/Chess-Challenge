using ChessChallenge.API;
using System;
using System.Diagnostics;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

        private void SendLine(string command)

        {
            Debug.WriteLine("[UCI SEND] " + command);
            myProcess.StandardInput.WriteLine(command);
            myProcess.StandardInput.Flush();
        }

        private void myProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            string text = e.Data;
            Debug.WriteLine("[UCI] " + text);
            if (text.StartsWith("bestmove"))
            {
                string[] splitText = text.Split(" ");
                mybestmove = new Move(splitText[1], mythinkingboard);
            }

        }

        public Process myProcess = null;
        public Move mybestmove = Move.NullMove;
        public Board mythinkingboard = null;

        public Move Think(Board board, Timer timer)
        {
            mythinkingboard = board;
            mybestmove = Move.NullMove;
            ProcessStartInfo si = new ProcessStartInfo()
            {
                FileName = "chispa403-p4.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            myProcess = new Process();
            myProcess.StartInfo = si;
            try
            {
                // throws an exception on win98
                myProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch { }

            myProcess.OutputDataReceived += new DataReceivedEventHandler(myProcess_OutputDataReceived);

            myProcess.Start();
            myProcess.BeginErrorReadLine();
            myProcess.BeginOutputReadLine();

            SendLine("uci");

            SendLine("isready");
            System.Threading.Thread.Sleep(200);
            SendLine("ucinewgame");
            System.Threading.Thread.Sleep(200);
            SendLine("position fen \"" + board.GetFenString() + "\"");
            System.Threading.Thread.Sleep(200);
            SendLine("go movetime 200");


            while (mybestmove == Move.NullMove)
            {
                System.Threading.Thread.Sleep(100);
            }

            return mybestmove;
        }

        // Test if this move gives checkmate
        bool MoveIsCheckmate(Board board, Move move)
        {
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }
    }
}