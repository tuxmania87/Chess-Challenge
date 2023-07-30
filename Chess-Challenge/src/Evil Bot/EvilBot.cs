using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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
            if (text != null && text.StartsWith("bestmove"))
            {
                string[] splitText = text.Split(" ");
                mybestmove = new Move(splitText[1], mythinkingboard);
                SendLine("quit");
            }

        }

        public Process myProcess = null;
        public Move mybestmove = Move.NullMove;
        public Board mythinkingboard = null;


        public Move Think(Board board, Timer timer)
        {
            return Think_tuxfish(board, timer);
        }



        public Move Think_GoodEngine(Board board, Timer timer)
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
            int moveTime = 1000;

            if (timer.MillisecondsRemaining < 30 * 1000)
                moveTime = 800;

            if (timer.MillisecondsRemaining < 20 * 1000)
                moveTime = 500;

            if (timer.MillisecondsRemaining < 10 * 1000)
                moveTime = 200;



            SendLine(string.Format("go movetime {0}", moveTime));


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

        public int _alphaBetaNodes = 0;
        public int _qNodes = 0;

        static int INF = 12000000;
        static Dictionary<PieceType, Dictionary<PieceType, int>> Mvv_Lva_Scores
            = new Dictionary<PieceType, Dictionary<PieceType, int>>();

        public Dictionary<PieceType, int> evalScores = new Dictionary<PieceType, int>()
    {
        { PieceType.Pawn, 100 },
        { PieceType.Knight, 300 },
        { PieceType.Bishop, 305},
        { PieceType.Rook, 500} ,
        { PieceType.King, 9000 },
        { PieceType.Queen, 900 },
        { PieceType.None, 0 }
    };

        public Dictionary<PieceType, int> Mvv_Lva_Victim_scores = new Dictionary<PieceType, int> {
            { PieceType.Pawn , 100 },
            { PieceType.Knight , 200 },
            { PieceType.Bishop , 300 },
            { PieceType.Rook , 400 },
            { PieceType.Queen , 500 },
            { PieceType.King , 600 }
        };

        public int evaluatePosition(Board board)
        {

            int score = 0;

            for (int i = 0; i < 64; i++)
            {
                Square s = new Square(i);
                Piece p = board.GetPiece(s);

                score += evalScores[p.PieceType] * (p.IsWhite ? 1 : -1);

                if (p.PieceType == PieceType.Knight || p.PieceType == PieceType.Bishop)
                {
                    if ((p.IsWhite && s.Rank == 0) || (!p.IsWhite && s.Rank == 7))
                        score -= 20 * (p.IsWhite ? 1 : -1);
                }
                else if (p.PieceType == PieceType.Rook && (s.File <= 1 || s.File >= 6))
                    score -= 20 * (p.IsWhite ? 1 : -1);
                else if (p.PieceType == PieceType.Pawn)
                {
                    if ((s.Rank == 1 || s.Rank == 2) && p.IsWhite && s.File >= 2 && s.File <= 4)
                        score -= 20 * (p.IsWhite ? 1 : -1);
                    if ((s.Rank == 5 || s.Rank == 6) && !p.IsWhite && s.File >= 2 && s.File <= 4)
                        score -= 20 * (p.IsWhite ? 1 : -1);
                }

            }

            return score * (board.IsWhiteToMove ? 1 : -1);
        }

        public class PVNode
        {
            public int cmove;
            //public Move[] line = new Move[10000]; 
            public List<Move> argmove = new List<Move>();
        }

        public int Quiesce(Board board, int alpha, int beta, int qdepth)
        {
            _qNodes++;

            int ev = evaluatePosition(board);

            if (qdepth >= 5)
                return ev;

            if (ev >= beta)
            {
                return beta;
            }
            if (alpha < ev)
            {
                alpha = ev;
            }

            Move[] allLegalMoves = board.GetLegalMoves(true);

            Dictionary<Move, int> scoredMoves = new Dictionary<Move, int>();
            foreach (Move m in allLegalMoves)
            {

                PieceType attacker = board.GetPiece(m.StartSquare).PieceType;
                PieceType victim = board.GetPiece(m.TargetSquare).PieceType;

                if (victim == PieceType.None)
                    victim = PieceType.Pawn;
                scoredMoves[m] = Mvv_Lva_Scores[attacker][victim];

            }

            List<Move> scoredList = scoredMoves.OrderBy(x => x.Value).Select(y => y.Key).ToList();
            scoredList.Reverse();

            foreach (Move m in scoredList)
            {
                board.MakeMove(m);
                int score = -1 * Quiesce(board, -beta, -alpha, qdepth + 1);
                board.UndoMove(m);

                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }
            return alpha;
        }

        public int AlphaBeta(Board board, int alpha, int beta, int depth, int maxdepth, PVNode pline, bool nullMove)
        {
            _alphaBetaNodes++;
            PVNode line = new PVNode();

            if (depth == 0)
            {
                pline.cmove = 0;
                return evaluatePosition(board);
                //return Quiesce(board, alpha, beta, 0);
            }

            Move[] allLegalMoves = board.GetLegalMoves();

            if (allLegalMoves.Length == 0)
            {
                if (board.IsInCheck())
                {
                    return -1 * (100000 + depth);
                }
                else
                {
                    return 0;
                }
            }
            if (board.IsDraw())
            {
                return 0;
            }

            if (board.IsInCheck())
            {
                depth++;
            }

            foreach (Move m in allLegalMoves)
            {
                board.MakeMove(m);
                //string tempfen = board.GetFenString();
                int score = -1 * AlphaBeta(board, -beta, -alpha, depth - 1, maxdepth, line, nullMove);
                board.UndoMove(m);

                if (score >= beta)
                    return beta;
                if (score > alpha)
                {
                    alpha = score;
                    List<Move> _mm = new List<Move>();
                    _mm.Add(m);
                    _mm.AddRange(line.argmove);
                    pline.argmove = _mm;
                    pline.cmove = line.cmove;
                }
            }

            return alpha;
        }

        public Move Think_tuxfish(Board board, Timer timer)
        {
            Console.WriteLine(string.Format("Timer {0}", timer.MillisecondsRemaining));
            _alphaBetaNodes = 0;
            _qNodes = 0;
            foreach (var attacker in Mvv_Lva_Victim_scores.Keys)
            {
                Mvv_Lva_Scores[attacker] = new Dictionary<PieceType, int>();
                foreach (var victim in Mvv_Lva_Victim_scores.Keys)
                {
                    Mvv_Lva_Scores[attacker][victim] = Convert.ToInt32((Mvv_Lva_Victim_scores[victim]) + 6 - (Mvv_Lva_Victim_scores[attacker] / 100)) + 1000000;
                }
            }

            int maxdepth = 5;

            if (timer.MillisecondsRemaining < 40 * 1000)
                maxdepth = 5;
            if (timer.MillisecondsRemaining < 12 * 1000)
                maxdepth = 4;


            PVNode pVNode = new PVNode();
            int score = AlphaBeta(board, -INF, INF, maxdepth, maxdepth, pVNode, true);

            Console.WriteLine(string.Format("Depth {0} Score {1} Nodes {2} QNodes {3}", maxdepth, score, _alphaBetaNodes, _qNodes));

            /*
            for(int i =0; i < maxdepth; i++)
            {
                PVNode pVNode = new PVNode();
                int score = AlphaBeta(board, -INF, INF, i, i, pVNode, true);
                //Console.WriteLine("Level {0} Score: {1}", i, score);
                if (pVNode.argmove.Count > 0 && pVNode.argmove[0] != Move.NullMove)
                {
                    bestMoveSoFar = pVNode.argmove[0];
                }
            }*/

            if (pVNode.argmove.Count > 0 && pVNode.argmove[0] != Move.NullMove)
                return pVNode.argmove[0];


            return board.GetLegalMoves()[0];
        }
    }



}