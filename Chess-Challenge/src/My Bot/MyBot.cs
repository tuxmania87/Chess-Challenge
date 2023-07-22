using ChessChallenge.API;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.InteropServices;

public class MyBot : IChessBot
{


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
        public int moveNumber;
        public List<Move> line = new List<Move>();
    }

    public int AlphaBeta(Board board, int alpha, int beta, int depth, int maxdepth)
    {
        if(depth == 0)
        {
            return evaluatePosition(board);
        }

        foreach (Move m in board.GetLegalMoves())
        {
            board.MakeMove(m);
            int score = -1 * AlphaBeta(board, -beta, -alpha, depth - 1, maxdepth);
            board.UndoMove(m);

            if (score >= beta)
                return beta;
            if (score > alpha)
            {
                alpha = score;

            }
        }

        return alpha;
    }

    public Move Think(Board board, Timer timer)
    {
        //Move[] moves = board.GetLegalMoves();
        PVNode pv = new PVNode();

        for (int i = 0; i < 20; i++)
        {
            int score = AlphaBeta(board, int.MinValue, int.MaxValue, i, i);
            Console.WriteLine(string.Format("Depth {0}: {1}", i, score));
        }

        

        return Move.NullMove;
    }
}