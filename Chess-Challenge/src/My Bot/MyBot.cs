﻿using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;


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

            if (p.PieceType == PieceType.Knight || p.PieceType == PieceType.Bishop)
            {
                if ((p.IsWhite && s.Rank == 0) || (!p.IsWhite && s.Rank == 7))
                    score -= 20 * (p.IsWhite ? 1 : -1);
            }
            else if (p.PieceType == PieceType.Rook && (s.File <= 1 || s.File >= 6))
                score -= 20 * (p.IsWhite ? 1 : -1);
            else if (p.PieceType == PieceType.Pawn)
            {
                if (s.Rank == 1 && p.IsWhite && s.File >= 2 && s.File <= 5)
                    score -= 20 * (p.IsWhite ? 1 : -1);
                if (s.Rank == 6 && !p.IsWhite && s.File >= 2 && s.File <= 5)
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

    public int AlphaBeta(Board board, int alpha, int beta, int depth, int maxdepth, PVNode pline, bool nullMove)
    {

        PVNode line = new PVNode();

        if (depth == 0)
        {
            pline.cmove = 0;
            return evaluatePosition(board);
            //return Quiesce(board, alpha, beta, board.PlyCount);
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

        Dictionary<Move, int> scoredMoves = new Dictionary<Move, int>();
        foreach (Move m in allLegalMoves)
        {
            if (m.IsCapture)
            {
                PieceType attacker = board.GetPiece(m.StartSquare).PieceType;
                PieceType victim = board.GetPiece(m.TargetSquare).PieceType;

                if (victim == PieceType.None)
                    victim = PieceType.Pawn;
                scoredMoves[m] = Mvv_Lva_Scores[attacker][victim];
            }
            else
                scoredMoves[m] = 2;
        }

        List<Move> scoredList = scoredMoves.OrderBy(x => x.Value).Select(y => y.Key).ToList();
        scoredList.Reverse();

        foreach (Move m in scoredList)
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
        Console.WriteLine(string.Format("{0} {1}", board.PlyCount, timer.MillisecondsRemaining));
        foreach (var attacker in Mvv_Lva_Victim_scores.Keys)
        {
            Mvv_Lva_Scores[attacker] = new Dictionary<PieceType, int>();
            foreach (var victim in Mvv_Lva_Victim_scores.Keys)
            {
                Mvv_Lva_Scores[attacker][victim] = Convert.ToInt32((Mvv_Lva_Victim_scores[victim]) + 6 - (Mvv_Lva_Victim_scores[attacker] / 100)) + 1000000;
            }
        }

        int maxdepth = 6;

        if (timer.MillisecondsRemaining < 30 * 1000)
            maxdepth = 5;
        else if (timer.MillisecondsRemaining < 12 * 1000)
            maxdepth = 4;


        PVNode pVNode = new PVNode();
        int score = AlphaBeta(board, -INF, INF, maxdepth, maxdepth, pVNode, true);


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