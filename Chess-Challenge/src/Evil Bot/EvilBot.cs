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
            return Think_Ferdi(board, timer);
        }

        public Move Think_sf(Board board, Timer timer)
        {
            int level = 3;
            mythinkingboard = board;
            mybestmove = Move.NullMove;
            ProcessStartInfo si = new ProcessStartInfo()
            {
                FileName = "stockfish_15_x64_avx2.exe",
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
            System.Threading.Thread.Sleep(2);

            SendLine(string.Format("setoption name Skill Level value {0}", level));
            System.Threading.Thread.Sleep(2);

            SendLine("ucinewgame");
            System.Threading.Thread.Sleep(2);
            SendLine("position fen \"" + board.GetFenString() + "\"");
            System.Threading.Thread.Sleep(2);
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

            // check if legal
            if(!board.GetLegalMoves().Contains(mybestmove))
            {
                return board.GetLegalMoves()[0];
            }

            return mybestmove;
        }

        public Move Think_oldengine(Board board, Timer timer)
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


        //FERDI

        float move_punisher = 1000f;
        float matt_value = -1000000f;


        private Move_Value_Pear select_random_move(List<Move_Value_Pear> list)
        {
            Random random = new Random();
            int randInt = random.Next(0, list.Count);
            return list[randInt];
        }

        public class Move_Value_Pear
        {
            public Move move { get; set; }
            public float value { get; set; }

        }
        private float evaluate_board(Board board)
        {
            if (board.IsDraw())
            {
                // coping on blunders from enemy
                return -0.1f;

            }
            else if (board.GetLegalMoves().Length == 0)
            {
                return matt_value;
            }
            else
            {
                float[] values = { 1, 3, 3.5f, 5, 9, 0 };
                PieceList[] pl = board.GetAllPieceLists();
                float white_score = 0.0f;
                float black_score = 0.0f;
                for (int i = 0; i < pl.Length / 2; i++)
                {
                    white_score += pl[i].Count * values[i];
                    black_score += pl[i + 6].Count * values[i];
                }
                white_score = (board.HasKingsideCastleRight(true) || board.HasQueensideCastleRight(true)) ? white_score + 1 : white_score;
                black_score = (board.HasKingsideCastleRight(false) || board.HasQueensideCastleRight(false)) ? black_score + 1 : black_score;
                return board.IsWhiteToMove ? white_score - black_score : -(white_score - black_score);
            }
        }



        private Move_Value_Pear alphabeta(Board board, int depth, float alpha, float beta, Boolean is_max_player, int depth_extender)
        {

            if (depth + depth_extender == 0 || board.GetLegalMoves().Length == 0)
            {
                Move_Value_Pear return_value = new Move_Value_Pear();
                float eval = evaluate_board(board);
                return_value.value = is_max_player ? eval : -eval;
                return return_value;
            }
            if (is_max_player)
            {
                List<Move_Value_Pear> movelist = new List<Move_Value_Pear>();
                float value = -(float.MaxValue);
                for (int i = 0; i < board.GetLegalMoves().Length; i++)
                {
                    Move temp_move = board.GetLegalMoves()[i];
                    board.MakeMove(temp_move);
                    Move_Value_Pear comp_move_and_value = alphabeta(board, depth - 1, alpha, beta, false, depth_extender);

                    if (comp_move_and_value.value > 20000f)
                    {
                        comp_move_and_value.value -= move_punisher;
                    }
                    comp_move_and_value.move = temp_move;

                    if (comp_move_and_value.value > value)
                    {
                        value = comp_move_and_value.value;
                        movelist = new List<Move_Value_Pear>() { comp_move_and_value };
                    }
                    else if (comp_move_and_value.value == value)
                    {
                        movelist.Add(comp_move_and_value);
                    }

                    board.UndoMove(temp_move);
                    if (value > beta)
                    {
                        break;
                    }
                    alpha = Math.Max(alpha, value);
                }
                return select_random_move(movelist);
            }
            else
            {
                depth_extender = depth_extender < 1 && board.IsInCheck() ? depth_extender + 1 : depth_extender;
                List<Move_Value_Pear> movelist = new List<Move_Value_Pear>();
                float value = float.MaxValue;
                for (int i = 0; i < board.GetLegalMoves().Length; i++)
                {
                    Move temp_move = board.GetLegalMoves()[i];
                    board.MakeMove(temp_move);
                    Move_Value_Pear comp_move_and_value = alphabeta(board, depth - 1, alpha, beta, true, depth_extender);

                    if (comp_move_and_value.value < -20000f)
                    {
                        comp_move_and_value.value += move_punisher;
                    }

                    comp_move_and_value.move = temp_move;

                    if (comp_move_and_value.value < value)
                    {
                        value = comp_move_and_value.value;
                        movelist = new List<Move_Value_Pear>() { comp_move_and_value };
                    }
                    else if (comp_move_and_value.value == value)
                    {
                        movelist.Add(comp_move_and_value);
                    }

                    board.UndoMove(temp_move);
                    if (value < alpha)
                    {
                        break;
                    }
                    beta = Math.Min(beta, value);
                }
                return select_random_move(movelist);
            }

        }

        public Move Think_Ferdi(Board board, Timer timer)
        {
            Move[] moves = board.GetLegalMoves();
            if (moves.Length == 1)
            {
                return moves[0];
            }

            int factor = 0;

            PieceList[] pl = board.GetAllPieceLists();
            float white_amount = 0;
            float black_amount = 0;

            for (int i = 0; i < pl.Length / 2; i++)
            {
                white_amount += pl[i].Count;
                black_amount += pl[i + 6].Count;
            }

            factor = white_amount < 2 ? factor + 1 : factor;
            factor = white_amount < 1 ? factor + 1 : factor;
            factor = black_amount < 2 ? factor + 1 : factor;
            factor = black_amount < 1 ? factor + 1 : factor;

            factor = timer.MillisecondsRemaining < 10000 ? factor / 2 : factor;


            Move_Value_Pear best = alphabeta(board, 4 + factor, -(float.MaxValue), float.MaxValue, true, 0);

            Console.WriteLine("depth: " + (4 + factor));

            if (best.value > 200000f)
            {
                Console.WriteLine("eval: #" + (-((matt_value + best.value) / move_punisher)));
            }
            else if (best.value < -200000f)
            {
                Console.WriteLine("eval: #" + (-((matt_value - best.value) / move_punisher)));
            }
            else
            {
                Console.WriteLine("eval: " + best.value);
            }
            Console.WriteLine(best.move);

            return best.move;
        }

    }
}