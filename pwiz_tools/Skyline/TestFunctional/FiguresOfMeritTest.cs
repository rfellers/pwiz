﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
    // ReSharper disable AccessToForEachVariableInClosure
    [TestClass]
    public class FiguresOfMeritTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFiguresOfMerit()
        {
            TestFilesZip = @"TestFunctional\FiguresOfMeritTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            int seed = (int) DateTime.Now.Ticks;
            Console.WriteLine("FiguresOfMeritTest: using random seed {0}", seed);
            var random = new Random(seed);
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("FiguresOfMeritTest.sky")));
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                documentGrid.ChooseView("FiguresOfMerit");
            });
            var calibrationForm = ShowDialog<CalibrationForm>(()=>SkylineWindow.ShowCalibrationForm());
            var results = new List<Tuple<FiguresOfMeritOptions, ModifiedSequence, FiguresOfMerit>>();
            int count = 0;
            foreach (var options in EnumerateFiguresOfMeritOptions().OrderBy(x=>random.Next()))
            {
                count++;
                bool doFullTest = count < 5;
                var newQuantification = SkylineWindow.Document.Settings.PeptideSettings.Quantification;
                newQuantification = newQuantification
                        .ChangeRegressionFit(options.RegressionFit)
                        .ChangeLodCalculation(options.LodCalculation)
                        .ChangeMaxLoqCv(options.MaxLoqCv)
                        .ChangeMaxLoqBias(options.MaxLoqBias);
                if (doFullTest)
                {
                    var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                    RunUI(() =>
                    {
                        peptideSettingsUi.QuantRegressionFit = options.RegressionFit;
                        peptideSettingsUi.QuantLodMethod = options.LodCalculation;
                        peptideSettingsUi.QuantMaxLoqBias = options.MaxLoqBias;
                        peptideSettingsUi.QuantMaxLoqCv = options.MaxLoqCv;
                    });
                    OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
                    if (!Equals(newQuantification, SkylineWindow.Document.Settings.PeptideSettings.Quantification))
                    {
                        Assert.AreEqual(newQuantification, SkylineWindow.Document.Settings.PeptideSettings.Quantification);
                    }
                }
                else
                {
                    RunUI(() =>
                    {
                        SkylineWindow.ModifyDocument("Test changed settings",
                            doc => doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                                doc.Settings.PeptideSettings.ChangeAbsoluteQuantification(newQuantification))));
                    });
                }
                WaitForConditionUI(() => documentGrid.IsComplete);
                var colPeptideModifiedSequence = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == ColumnCaptions.PeptideModifiedSequence);
                Assert.IsNotNull(colPeptideModifiedSequence);
                var colFiguresOfMerit = documentGrid.DataGridView.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(col => col.HeaderText == ColumnCaptions.FiguresOfMerit);
                Assert.IsNotNull(colFiguresOfMerit);
                var docContainer = new MemoryDocumentContainer();
                Assert.IsTrue(docContainer.SetDocument(SkylineWindow.Document, docContainer.Document));
                var dataSchema = new SkylineDataSchema(docContainer, SkylineDataSchema.GetLocalizedSchemaLocalizer());
                foreach (var group in SkylineWindow.Document.MoleculeGroups)
                {
                    foreach (var peptide in group.Molecules)
                    {
                        var identityPath = new IdentityPath(group.Id, peptide.Id);
                        var peptideEntity = new Skyline.Model.Databinding.Entities.Peptide(dataSchema, identityPath);
                        ValidateFiguresOfMerit(options, peptideEntity.FiguresOfMerit);
                        if (doFullTest)
                        {
                            RunUI(()=>SkylineWindow.SelectedPath = identityPath);
                            WaitForGraphs();
                        }
                    }
                }
            }
            foreach (var result in results)
            {
                foreach (var resultCompare in results)
                {
                    if (!Equals(result.Item2, resultCompare.Item2))
                    {
                        continue;
                    }
                    var options1 = result.Item1;
                    var options2 = resultCompare.Item1;
                    if (!Equals(options1.RegressionFit, options2.RegressionFit))
                    {
                        continue;
                    }
                    CompareLoq(result.Item1, result.Item3, resultCompare.Item1, resultCompare.Item3);
                }
            }
        }

        private IEnumerable<FiguresOfMeritOptions> EnumerateFiguresOfMeritOptions()
        {
            foreach (var regressionFit in new[] {RegressionFit.NONE, RegressionFit.LINEAR, RegressionFit.BILINEAR})
            {
                foreach (var lodCalculation in new[]
                {
                    LodCalculation.NONE, LodCalculation.TURNING_POINT, LodCalculation.BLANK_PLUS_2SD,
                    LodCalculation.BLANK_PLUS_3SD
                })
                {
                    foreach (var maxLoqBias in new double?[] {null, 0, 20, 1e6})
                    {
                        foreach (var maxLoqCv in new double?[] {null, 0, 20, 1e6})
                        {
                            yield return new FiguresOfMeritOptions
                            {
                                RegressionFit = regressionFit,
                                LodCalculation = lodCalculation,
                                MaxLoqBias = maxLoqBias,
                                MaxLoqCv = maxLoqCv
                            };
                        }
                    }
                }
            }
        }

        private void ValidateFiguresOfMerit(FiguresOfMeritOptions options, FiguresOfMerit figuresOfMerit)
        {
            if (options.MaxLoqBias.HasValue || options.MaxLoqCv.HasValue)
            {
                double min = Math.Min(options.MaxLoqBias.GetValueOrDefault(1000),
                    options.MaxLoqCv.GetValueOrDefault(1000));
                if (min <= 0)
                {
                    Assert.IsNull(figuresOfMerit.LimitOfQuantification);
                }
                if (min >= 1000)
                {
                    if (!Equals(RegressionFit.NONE, options.RegressionFit) || !options.MaxLoqBias.HasValue)
                    {
                        Assert.IsNotNull(figuresOfMerit.LimitOfQuantification);
                    }
                }
            }
            else
            {
                Assert.IsNull(figuresOfMerit.LimitOfQuantification);
            }
            if (options.LodCalculation == LodCalculation.TURNING_POINT)
            {
                if (options.RegressionFit == RegressionFit.BILINEAR)
                {
                    Assert.IsNotNull(figuresOfMerit.LimitOfDetection);
                }
                else
                {
                    Assert.IsNull(figuresOfMerit.LimitOfDetection);
                }
            }
        }

        /// <summary>
        /// Asserts that if options1 uses less stringent criteria for calculating LOQ, then that
        /// must results in a lower resulting LOQ value.
        /// </summary>
        private void CompareLoq(FiguresOfMeritOptions options1, FiguresOfMerit result1, FiguresOfMeritOptions options2,
            FiguresOfMerit result2)
        {
            if (!options1.MaxLoqBias.HasValue && !options1.MaxLoqCv.HasValue)
            {
                return;
            }
            // We only care about cases where options1 is stricter than options2
            if (options1.MaxLoqBias.GetValueOrDefault(double.MaxValue) <= options2.MaxLoqBias.GetValueOrDefault(double.MaxValue))
            {
                return;
            }
            if (options1.MaxLoqCv.GetValueOrDefault(Double.MaxValue) <=
                Math.Min(options2.MaxLoqBias.GetValueOrDefault(double.MaxValue),
                    options2.MaxLoqCv.GetValueOrDefault(double.MaxValue)))
            {
                return;
            }

            // we have determined that options1 is more lenient than options2
            Assert.IsTrue(result1.LimitOfQuantification.GetValueOrDefault(double.MaxValue) 
                <= result2.LimitOfQuantification.GetValueOrDefault(double.MaxValue));
        }

        struct FiguresOfMeritOptions
        {
            public RegressionFit RegressionFit;
            public LodCalculation LodCalculation;
            public double? MaxLoqBias;
            public double? MaxLoqCv;
        }
    }
}