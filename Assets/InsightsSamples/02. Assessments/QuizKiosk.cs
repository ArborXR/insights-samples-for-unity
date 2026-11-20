using System;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Assessments
{
    /// <summary>
    /// A site induction kiosk with three questions. It scores the quiz; the lesson script decides what the
    /// score means.
    /// </summary>
    public class QuizKiosk : MonoBehaviour
    {
        private struct Question
        {
            public string Text;
            public string[] Answers;
            public int Correct;
        }

        private static readonly Question[] Questions =
        {
            new Question { Text = "The fire alarm sounds. Where do you go?", Answers = new[] { "Back to my desk", "The assembly point", "The break room" }, Correct = 1 },
            new Question { Text = "A spill is blocking a walkway. What do you do first?", Answers = new[] { "Warn others and report it", "Step around it", "Leave it for the cleaners" }, Correct = 0 },
            new Question { Text = "When must you wear safety glasses?", Answers = new[] { "Only when using power tools", "Only when it's dusty", "In any marked eye-protection area" }, Correct = 2 },
        };

        [SerializeField] private Material material;
        [SerializeField] private GameObject ppeBoardModel;

        private readonly List<StationInteractable> _answers = new List<StationInteractable>();
        private TextMeshPro _screen;
        private int _index = -1;
        private int _correct;

        public event Action QuizStarted;
        public event Action<int> QuizFinished;

        public StationInteractable StartButton { get; private set; }
        public IReadOnlyList<StationInteractable> AnswerButtons => _answers;
        public bool InQuiz => _index >= 0 && _index < Questions.Length;
        public int CorrectAnswerIndex => InQuiz ? Questions[_index].Correct : -1;

        private void Awake()
        {
            StationBuilder.Kiosk(transform, new Vector3(0, 1.21f, 0.052f), new Vector3(1.3f, 1.08f, 0.08f), new Vector3(0, 1.47f, 0.01f), new Vector2(1.15f, 0.5f), material);
            _screen = StationBuilder.Text(transform, "", new Vector3(0, 1.47f, 0.0f), 0.5f, Color.white, 1.05f, 0.45f);

            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var answer = StationBuilder.Button(transform, "", new Vector3(0, 1.11f - i * 0.17f, 0.0f), new Vector2(1.05f, 0.14f), material, UiKit.Purple, 0.4f);
                answer.Activated += _ => Answer(index);
                _answers.Add(answer);
            }

            StartButton = StationBuilder.Button(transform, "Start quiz", new Vector3(0, 0.94f, 0.0f), new Vector2(0.6f, 0.16f), material, UiKit.Purple, 0.45f);
            StartButton.Activated += _ => StartQuiz();
            BuildPpeBoard();
            ShowIdle();
        }

        public void ShowOutcome(bool passed, int score)
        {
            _screen.text = $"Score: {score}\n{(passed ? "<color=#05DA98>Passed</color>" : "<color=#F5B841>Not passed</color>")}";
            SetAnswersVisible(false);
            StartButton.GetComponentInChildren<TextMeshPro>().text = "Retake quiz";
            StartButton.gameObject.SetActive(true);
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void ShowIdle()
        {
            _screen.text = "<b>SITE SAFETY INDUCTION</b>\n<size=80%>Three questions. Pass mark: 70.</size>";
            SetAnswersVisible(false);
        }

        /// <summary>Set dressing beside the kiosk: the PPE the questions ask about.</summary>
        private void BuildPpeBoard()
        {
            if (ppeBoardModel == null) return;
            var board = StationBuilder.Model(transform, "PPE Board", ppeBoardModel, new Vector3(-1.45f, 0, 0.45f), Vector3.one).transform;
            board.localRotation = Quaternion.Euler(0, -15f, 0);
            StationBuilder.Text(board, "<b>PPE STATION</b>", new Vector3(0, 1.85f, -0.014f), 0.42f, Color.white, 1.2f, 0.07f);
        }

        private void StartQuiz()
        {
            _index = 0;
            _correct = 0;
            StartButton.gameObject.SetActive(false);
            ShowQuestion();
            QuizStarted?.Invoke();
        }

        private void Answer(int choice)
        {
            if (!InQuiz) return;
            if (choice == Questions[_index].Correct) _correct++;
            _index++;

            if (InQuiz)
            {
                ShowQuestion();
                return;
            }

            SetAnswersVisible(false);
            QuizFinished?.Invoke(Mathf.RoundToInt(100f * _correct / Questions.Length));
        }

        private void ShowQuestion()
        {
            var question = Questions[_index];
            _screen.text = $"<size=75%>Question {_index + 1} of {Questions.Length}</size>\n{question.Text}";
            for (var i = 0; i < _answers.Count; i++)
                _answers[i].GetComponentInChildren<TextMeshPro>().text = question.Answers[i];
            SetAnswersVisible(true);
        }

        private void SetAnswersVisible(bool visible)
        {
            foreach (var answer in _answers) answer.gameObject.SetActive(visible);
        }
    }
}
