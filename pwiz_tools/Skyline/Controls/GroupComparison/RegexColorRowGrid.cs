﻿/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.ToolsUI;

namespace pwiz.Skyline.Controls.GroupComparison
{
    /// <summary>
    /// Wrapper class for ColorGrid so that forms using it can be designed.
    /// </summary>
    public class RegexColorRowGrid : ColorGrid<MatchRgbHexColor>
    {
    }
}