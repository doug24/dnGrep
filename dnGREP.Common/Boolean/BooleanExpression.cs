﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace dnGREP.Common
{
    public class BooleanExpression
    {
        public ParserErrorState ParserState { get; private set; } = ParserErrorState.None;

        /// <summary>
        /// Gets a simplified expression logically the same as the parser input string
        /// </summary>
        public string Expression { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the parser input string in postfix order
        /// </summary>
        public string PostfixExpression { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the parsed input as list of tokens in postfix order
        /// </summary>
        public List<BooleanToken> PostfixTokens { get; private set; } = [];

        /// <summary>
        /// Gets the list of operands from the parsed input
        /// </summary>
        public List<BooleanToken> Operands => PostfixTokens.Where(r => r.IsOperand).ToList();

        /// <summary>
        /// Returns true if at least one operator is an "OR"
        /// </summary>
        public bool HasOrExpression => PostfixTokens.Where(r => r.TokenType == TokenType.OR || r.TokenType == TokenType.XOR).Any();

        /// <summary>
        /// Returns true if all the operands have results
        /// </summary>
        public bool IsComplete => Operands.All(o => o.EvaluatedResult.HasValue);

        /// <summary>
        /// Evaluates the expression if all the operands have results
        /// </summary>
        /// <returns></returns>
        public EvaluationResult Evaluate()
        {
            EvaluationResult result = EvaluationResult.Undetermined;

            try
            {
                if (IsComplete)
                {
                    result = EvaluateExpression(true) ? EvaluationResult.True : EvaluationResult.False;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error evaluating expression: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Tests if the expression will evaluate False with any combination of remaining inputs
        /// </summary>
        /// <returns>true if all possible combinations result in a False evaluation</returns>
        /// <remarks>
        /// This determines if the search for operands may be halted before the end of the list.
        /// For example, the expression 'a AND b' can be halted if 'a' is false because it does not
        /// matter what 'b' is. Conversely, the expression 'a OR b' cannot be halted if 'a' is false 
        /// since 'b' can be true. Even if the expression is known to be True before evaluating all
        /// the operands, processing operands should continue to find all the matches in the remaining 
        /// operands.
        /// </remarks>
        public bool IsShortCircuitFalse()
        {
            // initial checks
            List<BooleanToken> operators = PostfixTokens.Where(t => t.IsOperator).ToList();
            if (operators.Last().TokenType.HasFlag(TokenType.OR))
            {
                return false;  // must collect and evaluate all operands
            }

            int count = 0;
            foreach (BooleanToken token in operators)
            {
                if (token.TokenType.HasFlag(TokenType.AND))
                    count++;
                else
                    break;
            }
            for (int idx = 0; idx < count; idx++)
            {
                var op = Operands[idx];
                if (op.EvaluatedResult.HasValue && !op.EvaluatedResult.Value)
                {
                    return true; // the first operand of an AND is false, the full statement cannot be true
                }
            }

            // do not build a truth table larger than 2^^5, it's too slow
            if (Operands.Count > 5)
            {
                return false;
            }

            // now build a truth table and try all the possible outcomes
            // no doubt there is some clever way of doing this
            var savedState = Operands.Select(o => o.EvaluatedResult).ToList();

            List<List<bool>> values = [];
            for (int idx = 0; idx < Math.Pow(2, Operands.Count); idx++)
            {
                values.Add([]);
                string binary = Convert.ToString(idx, 2).PadLeft(Operands.Count, '0');

                for (int jdx = 0; jdx < Operands.Count; jdx++)
                {
                    var b = binary[jdx];
                    values[idx].Add(b != '0');
                }
            }

            EvaluationResult result = EvaluationResult.Undetermined;

            foreach (var row in values)
            {
                for (int col = 0; col < row.Count; col++)
                {
                    if (!Operands[col].EvaluatedResult.HasValue)
                    {
                        Operands[col].EvaluatedResult = row[col];
                    }
                }

                EvaluationResult rowResult = EvaluateExpression(false) ? EvaluationResult.True : EvaluationResult.False;

                // restore original state
                for (int col = 0; col < Operands.Count; col++)
                {
                    Operands[col].EvaluatedResult = savedState[col];
                }

                if (result == EvaluationResult.Undetermined)
                {
                    result = rowResult;
                }
                else if (result != rowResult)
                {
                    return false;
                }
            }

            return result == EvaluationResult.False;
        }

        /// <summary>
        /// Tests if the expression will evaluate to true with all negative inputs
        /// </summary>
        /// <returns></returns>
        public bool IsNegativeExpression()
        {
            bool result = false;
            var savedState = Operands.Select(o => o.EvaluatedResult).ToList();

            foreach (var op in Operands)
            {
                op.EvaluatedResult = false;
            }

            result = EvaluateExpression(false);

            // restore original state
            for (int col = 0; col < Operands.Count; col++)
            {
                Operands[col].EvaluatedResult = savedState[col];
            }

            return result;
        }

        private bool EvaluateExpression(bool modifyMatches)
        {
            Stack<bool> operandStack = new();
            // this stack is used to remove matches from sub-expressions
            // that evaluate to false.
            Stack<List<BooleanToken>> tokens = new();

            foreach (var token in PostfixTokens)
            {
                switch (token.TokenType)
                {
                    case TokenType.StringValue:
                        if (token.EvaluatedResult.HasValue)
                        {
                            operandStack.Push(token.EvaluatedResult.Value);
                            if (modifyMatches)
                            {
                                tokens.Push([token]);
                            }
                        }
                        else
                        {
                            throw new Exception("Expression is incomplete");
                        }
                        break;
                    case TokenType.NOT:
                        {
                            bool a = operandStack.Pop();
                            operandStack.Push(!a);

                            if (modifyMatches)
                            {
                                var bta = tokens.Pop();
                                if (operandStack.Peek() == false)
                                {
                                    bta.ForEach(x => x.Matches = null);
                                }
                                tokens.Push(bta);
                            }
                        }
                        break;
                    case TokenType.AND:
                        {
                            bool b = operandStack.Pop();
                            bool a = operandStack.Pop();
                            operandStack.Push(a && b);

                            if (modifyMatches)
                            {
                                var btb = tokens.Pop();
                                var bta = tokens.Pop();
                                if (operandStack.Peek() == false)
                                {
                                    btb.ForEach(x => x.Matches = null);
                                    bta.ForEach(x => x.Matches = null);
                                }
                                tokens.Push([.. btb, .. bta]);
                            }
                        }
                        break;
                    case TokenType.NAND:
                        {
                            bool b = operandStack.Pop();
                            bool a = operandStack.Pop();
                            operandStack.Push(!(a && b));

                            if (modifyMatches)
                            {
                                var btb = tokens.Pop();
                                var bta = tokens.Pop();
                                if (operandStack.Peek() == true)
                                {
                                    btb.ForEach(x => x.Matches = null);
                                    bta.ForEach(x => x.Matches = null);
                                }
                                tokens.Push([.. btb, .. bta]);
                            }
                        }
                        break;
                    case TokenType.XOR:
                        {
                            bool b = operandStack.Pop();
                            bool a = operandStack.Pop();
                            operandStack.Push((a || b) && !(a && b));

                            if (modifyMatches)
                            {
                                var btb = tokens.Pop();
                                var bta = tokens.Pop();
                                tokens.Push([.. btb, .. bta]);
                            }
                        }
                        break;
                    case TokenType.OR:
                        {
                            bool b = operandStack.Pop();
                            bool a = operandStack.Pop();
                            operandStack.Push(a || b);

                            if (modifyMatches)
                            {
                                var btb = tokens.Pop();
                                var bta = tokens.Pop();
                                tokens.Push([.. btb, .. bta]);
                            }
                        }
                        break;
                    case TokenType.NOR:
                        {
                            bool b = operandStack.Pop();
                            bool a = operandStack.Pop();
                            operandStack.Push(!(a || b));

                            if (modifyMatches)
                            {
                                var btb = tokens.Pop();
                                var bta = tokens.Pop();
                                if (operandStack.Peek() == false)
                                {
                                    btb.ForEach(x => x.Matches = null);
                                    bta.ForEach(x => x.Matches = null);
                                }
                                tokens.Push([.. btb, .. bta]);
                            }
                        }
                        break;
                }
            }
            return operandStack.Pop();
        }

        public bool TryParse(string input)
        {
            ParserState = ParserErrorState.None;
            bool result = true;
            try
            {
                BooleanTokenizer tokenizer = new();
                var list = tokenizer.Tokenize(input).ToList();

                result = BuildExpression(list);

                PostfixTokens = InfixToPostfix(list).ToList();
                PostfixExpression = string.Join(" ", PostfixTokens.Select(t => t.Value));

                int numUnaryOperators = PostfixTokens.Where(t => t.TokenType == TokenType.NOT).Count();
                int numBinaryOperators = PostfixTokens.Where(t => TokenType.Operator.HasFlag(t.TokenType)).Count() - numUnaryOperators;
                int numOperands = Operands.Count;

                if (numBinaryOperators < numOperands - 1)
                {
                    ParserState = ParserErrorState.MissingOperator;
                    result = false;
                }
            }
            catch (InvalidStateException ex)
            {
                ParserState = ex.State;
                result = false;
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                result = false;
                ParserState = ParserErrorState.UnknownError;
            }

            return result;
        }

        // the shunting-yard algorithm
        private static IEnumerable<BooleanToken> InfixToPostfix(IEnumerable<BooleanToken> tokens)
        {
            if (tokens.Count(t => t.TokenType == TokenType.OpenParens) !=
                tokens.Count(t => t.TokenType == TokenType.CloseParens))
            {
                throw new InvalidStateException(ParserErrorState.MismatchedParentheses);
            }

            if (tokens.Any() && TokenType.Operator.HasFlag(tokens.Last().TokenType))
            {
                throw new InvalidStateException(ParserErrorState.MissingOperand);
            }

            Stack<BooleanToken> stack = new();
            TokenType previousToken = TokenType.NotDefined;
            foreach (var token in tokens)
            {
                switch (token.TokenType)
                {
                    case TokenType.StringValue:
                        yield return token;
                        break;

                    case TokenType.AND:
                    case TokenType.NAND:
                    case TokenType.XOR:
                    case TokenType.OR:
                    case TokenType.NOR:
                        if (previousToken == TokenType.StringValue || previousToken == TokenType.CloseParens)
                        {
                            while (stack.Count != 0 && token.IsLowerPrecedence(stack.Peek()))
                            {
                                yield return stack.Pop();
                            }
                            stack.Push(token);
                        }
                        else
                        {
                            throw new InvalidStateException(ParserErrorState.MissingOperand);
                        }
                        break;

                    case TokenType.NOT:
                        while (stack.Count != 0 && token.IsLowerPrecedence(stack.Peek()))
                        {
                            yield return stack.Pop();
                        }
                        stack.Push(token);
                        break;

                    case TokenType.OpenParens:
                        stack.Push(token);
                        break;

                    case TokenType.CloseParens:
                        if (TokenType.Operator.HasFlag(previousToken))
                        {
                            throw new InvalidStateException(ParserErrorState.MismatchedParentheses);
                        }
                        while (stack.Peek().TokenType != TokenType.OpenParens)
                        {
                            yield return stack.Pop();
                        }
                        stack.Pop();
                        break;

                    default:
                        throw new InvalidStateException(ParserErrorState.UnknownToken);
                }
                previousToken = token.TokenType;
            }
            while (stack.Count != 0)
            {
                var token = stack.Pop();
                yield return token;
            }
        }

        private bool BuildExpression(IEnumerable<BooleanToken> tokens)
        {
            StringBuilder sb = new();
            int idx = 0;
            foreach (BooleanToken token in tokens)
            {
                if (token.TokenType == TokenType.StringValue)
                {
                    if (!string.IsNullOrWhiteSpace(token.Value))
                    {
                        sb.Append(GetId(idx++));
                    }
                }
                else if (token.TokenType == TokenType.OpenParens)
                {
                    sb.Append(" ( ");
                }
                else if (token.TokenType == TokenType.CloseParens)
                {
                    sb.Append(" ) ");
                }
                else if (TokenType.Operator.HasFlag(token.TokenType))
                {
                    if (token.Value.StartsWith(')'))
                    {
                        sb.Append(" ) ");
                    }
                    sb.Append($" {token.TokenType} ");
                    if (token.Value.EndsWith('('))
                    {
                        sb.Append(" ( ");
                    }
                }
            }

            Expression = sb.ToString().Replace("  ", " ", StringComparison.Ordinal).Trim();

            return true;
        }

        private static string GetId(int idx)
        {
            const char a = 'a';
            int n = idx % 26;
            if (idx < 26)
            {
                return char.ToString((char)(a + n));
            }
            else
            {
                int m = (idx / 26) - 1;
                return char.ToString((char)(a + m)) + char.ToString((char)(a + n));
            }
        }
    }

    public enum ParserErrorState
    {
        None = 0,
        MismatchedParentheses,
        MissingOperator,
        MissingOperand,
        UnknownToken,
        UnknownError,
    }

    public enum EvaluationResult
    {
        Undetermined = -1,
        False = 0,
        True = 1,
    }

    public class InvalidStateException(ParserErrorState state) : Exception(state.ToString())
    {
        public ParserErrorState State { get; private set; } = state;
    }
}
