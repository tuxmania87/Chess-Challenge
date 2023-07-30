using ChessChallenge.API;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using static MyBot;

public class MyBot : IChessBot
{
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
        }
        
        return score * (board.IsWhiteToMove ? 1 : -1);
    }

    public class PVNode
    {
        public int cmove;
        //public Move[] line = new Move[10000]; 
        public List<Move> argmove = new List<Move>();
    }

    public int Quiesce(Board board, int alpha, int beta)
    {
       

        int ev = evaluatePosition(board);
        if(ev >= beta)
        {
            return beta;
        }
        if (alpha < ev)
        {
            alpha = ev;
        }

        foreach(Move m in board.GetLegalMoves(true))
        {
            board.MakeMove(m);
            int score = -1 * Quiesce(board, -beta, -alpha);
            board.UndoMove(m);

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }


    public class HashEntry
    {
        public ulong key { get; set; }
        public Move move { get; set; }
        public int score { get; set; }
        public enum FLAG { ALPHA, BETA, EXACT, NONE }
        public FLAG flag { get; set; }
        public int depth { get; set; }
        public int md { get; set; }
    }


    public static uint hashtableSize = 2000000;
    //public static Dictionary<ulong, HashEntry> globalHashDict = new Dictionary<ulong, HashEntry>();
    public static HashEntry[] globalHashDict = new HashEntry[hashtableSize];

    public void StoreHashEntry(ulong key, Move move, int score, HashEntry.FLAG flag, int depth, int md)
    {

        HashEntry _e = new HashEntry();
        _e.key = key;
        _e.move = move;
        _e.score = score;
        _e.flag = flag;
        _e.depth = depth;
        _e.md = md;

        ulong index = key % hashtableSize;
        globalHashDict[index] = _e;

    }

    public HashEntry GetHashEntry(ulong key)
    {
        ulong index = key % hashtableSize;
        if (globalHashDict[index] != null && globalHashDict[index].key == key)

        {
            return globalHashDict[index];
        }
        return null;
    }

    public List<Move> get_pv_line_(Board board)
    {
        List<Move> ret = new List<Move>();

        Board _p = Board.CreateBoardFromFEN(board.GetFenString());

        while (true)
        {
            ulong kk = _p.ZobristKey;
            HashEntry _entry = GetHashEntry(kk);
            if (_entry == null || _p.IsDraw())
            {
                break;
            }
            ret.Add(_entry.move);
            _p.MakeMove(_entry.move);
        }
        return ret;

    }

    public int AlphaBeta(Board board, int alpha, int beta, int depth, int maxdepth)
    {

        int oldalpha = alpha;

        ulong kk = board.ZobristKey;
        HashEntry hashe = GetHashEntry(kk);

        if (hashe != null)
        {
            if(hashe.depth >= depth)
            {
                if (hashe.flag == HashEntry.FLAG.ALPHA && hashe.score <= alpha)
                {
                    return alpha;
                }
                if (hashe.flag == HashEntry.FLAG.BETA && hashe.score >= beta)
                {
                    return beta;
                }
                if (hashe.flag == HashEntry.FLAG.EXACT)
                {
                    return hashe.score;
                }
            }
        }

        if(depth == 0)
        {
            return evaluatePosition(board);
            //return Quiesce(board, alpha, beta);
        }

        Move[] allLegalMoves = board.GetLegalMoves();


        if(allLegalMoves.Length == 0)
        {
            if(board.IsInCheck())
            {
                return -1 * (100000 + depth);
            }
        }
        if(board.IsDraw())
        {
            return 0;
        }

        if(board.IsInCheck())
        {
            depth++;
        }

        int best_score = -INF;
        Move best_move = Move.NullMove;

        foreach (Move m in allLegalMoves)
        {
            board.MakeMove(m);
            //string tempfen = board.GetFenString();
            int score = -1 * AlphaBeta(board, -beta, -alpha, depth - 1, maxdepth);
            board.UndoMove(m);

            if (score > best_score)
            {
                best_score = score;
                best_move = m;

                if (score > alpha)
                {
                    if (score >= beta)
                    {
                        
                        StoreHashEntry(board.ZobristKey, best_move, beta, HashEntry.FLAG.BETA, depth, maxdepth);
                        return beta;
                    }

                    alpha = score;
                }
            }

        }


        if (oldalpha != alpha)
        {
            StoreHashEntry(kk, best_move, best_score, HashEntry.FLAG.EXACT, depth, maxdepth);
        }
        else
        {
            StoreHashEntry(kk, best_move, alpha, HashEntry.FLAG.ALPHA, depth, maxdepth);
        }

        return alpha;
    }

    public void iterative_deepening(Board board, int maxdepth)
    {
        for(int i= 0; i < maxdepth;i++)
        {
            int score = AlphaBeta(board, -INF, INF, i, i);
            Console.WriteLine(string.Format("Level {0} Score {1}", i, score));
        }
    }

    public Move Think(Board board, Timer timer)
    {


        foreach (var attacker in Mvv_Lva_Victim_scores.Keys)
        {
            Mvv_Lva_Scores[attacker] = new Dictionary<PieceType, int>();
            foreach (var victim in Mvv_Lva_Victim_scores.Keys)
            {
                Mvv_Lva_Scores[attacker][victim] = Convert.ToInt32((Mvv_Lva_Victim_scores[victim]) + 6 - (Mvv_Lva_Victim_scores[attacker] / 100)) + 1000000;
            }
        }

        //int score = AlphaBeta(board, -INF, INF, 4, 4);
        //Console.WriteLine("Score: {0}", score);
        iterative_deepening(board, 4);



        /*if(pVNode.argmove.Count == 0)
        {
            Random r = new Random();
            Move[] legal = board.GetLegalMoves();
            return legal[r.NextInt64(legal.Length)];
        }*/

        //return pVNode.argmove[0];
        return get_pv_line_(board)[0];
    }
}