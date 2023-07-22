using ChessChallenge.API;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;

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
        
        return score;
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


    public int AlphaBeta(Board board, int alpha, int beta, int depth, int maxdepth, PVNode pline)
    {

        PVNode line = new PVNode();

        if(depth == 0)
        {
            pline.cmove = 0;
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
            else
            {
                return 0;
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

        foreach (Move m in allLegalMoves)
        {
            board.MakeMove(m);
            //string tempfen = board.GetFenString();
            int score = -1 * AlphaBeta(board, -beta, -alpha, depth - 1, maxdepth, line);
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
                pline.cmove =  line.cmove;
            }
        }

        return alpha;
    }

    public string get_moves(Move[] moves)
    {
        string s = "";
        int anchor = 0;

        while (moves[anchor] != Move.NullMove)
        {
            s += moves[anchor++] + " ";
        }
        return s;
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

        PVNode pVNode = new PVNode();
        int score = AlphaBeta(board, -INF, INF, 4, 4, pVNode);

        Console.WriteLine("Score: {0}", score);

        if(pVNode.argmove.Count == 0)
        {
            Random r = new Random();
            Move[] legal = board.GetLegalMoves();

            return legal[r.NextInt64(legal.Length)];


        }

        return pVNode.argmove[0];
    }
}