﻿using ChessChallenge.API;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;


public class MyBot : IChessBot
{
    public MyBot() {
        
        foreach (var attacker in Mvv_Lva_Victim_scores.Keys)
        {
            Mvv_Lva_Scores[attacker] = new Dictionary<PieceType, int>();
            foreach (var victim in Mvv_Lva_Victim_scores.Keys)
            {
                Mvv_Lva_Scores[attacker][victim] = Convert.ToInt32((Mvv_Lva_Victim_scores[victim]) + 6 - (Mvv_Lva_Victim_scores[attacker] / 100)) + 1000000;
            }
        }

        history = new int[64][];
        for(int i=0; i<history.Length; i++)
        {
            history[i] = new int[64];
        }

    }

    public int[][] history = null;
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
                if ((s.Rank == 1 || s.Rank==2) && p.IsWhite && s.File >= 2 && s.File <= 4)
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

        if (qdepth >= 3)
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


        // sorting

        Dictionary<Move, int> scoredMoves = new Dictionary<Move, int>();

        foreach (Move m in allLegalMoves)
        {
            if(pline.argmove.Contains(m))
            {
                scoredMoves[m] = 10000000;
                continue;
            }

            if(m.IsCapture)
            {
                PieceType attacker = board.GetPiece(m.StartSquare).PieceType;
                PieceType victim = board.GetPiece(m.TargetSquare).PieceType;

                if (victim == PieceType.None)
                    victim = PieceType.Pawn;
                scoredMoves[m] = Mvv_Lva_Scores[attacker][victim];
            }
            else
            {
                scoredMoves[m] = history[m.StartSquare.Index][m.TargetSquare.Index];
            }
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


                if (!m.IsCapture)
                {
                    history[m.StartSquare.Index][m.TargetSquare.Index] += depth;
                }

            }
        }

        return alpha;
    }
    
    public Move Think(Board board, Timer timer)
    {
        _alphaBetaNodes = 0;
        _qNodes = 0;
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