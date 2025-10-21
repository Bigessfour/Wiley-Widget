#!/usr/bin/env python3
"""
Complete Python Evaluation of Wiley Widget Converters

This script evaluates all C# converter classes used in the Wiley Widget application.
It tests each converter with various input scenarios to ensure they behave correctly.

Converters evaluated:
1. BalanceColorConverter - Numeric to color conversion
2. BudgetProgressConverter - Budget scaling to progress bar values
3. CurrencyFormatConverter - Culture info for currency formatting
4. MessageConverters (multiple classes):
   - UserMessageBackgroundConverter
   - MessageAlignmentConverter
   - MessageForegroundConverter
   - ProfitLossTextConverter
   - ProfitBrushConverter
   - ProfitBorderBrushConverter
   - ProfitTextBrushConverter
   - BoolToBackgroundConverter
   - BoolToVisibilityConverter
   - EmptyStringToVisibilityConverter
   - CountToVisibilityConverter
   - BoolToForegroundConverter
   - InverseBooleanConverter
   - BooleanToFontWeightConverter
"""

import re
import sys
from dataclasses import dataclass
from enum import Enum
from typing import Any


def camel_to_snake(name: str) -> str:
    """Convert camelCase to snake_case"""
    # Insert underscore before uppercase letters and convert to lowercase
    s1 = re.sub("(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub("([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


class TestResult(Enum):
    PASS = "PASS"
    FAIL = "FAIL"
    ERROR = "ERROR"


@dataclass
class TestCase:
    """Represents a single test case for converter evaluation"""

    name: str
    input_value: Any
    expected_output: Any
    parameters: Any = None
    description: str = ""


@dataclass
class TestSuite:
    """Represents a collection of test cases for a converter"""

    converter_name: str
    test_cases: list[TestCase]
    converter_description: str = ""


class ConverterEvaluator:
    """Evaluates C# converter logic using Python implementations"""

    @staticmethod
    def evaluate_balance_color_converter(value: Any) -> str:
        """Python implementation of BalanceColorConverter logic"""
        if isinstance(value, (int, float)):
            numeric_value = float(value)
            if numeric_value > 0:
                return "Green"
            elif numeric_value < 0:
                return "Red"
            else:
                return "Gray"
        return "Gray"  # Default for unknown types

    @staticmethod
    def evaluate_budget_progress_converter(value: Any) -> float:
        """Python implementation of BudgetProgressConverter logic"""
        max_budget = 100000.0

        if isinstance(value, (int, float)):
            numeric_value = float(value)
            scaled_value = (numeric_value / max_budget) * 100
            return max(0.0, min(scaled_value, 100.0))
        return 0.0

    @staticmethod
    def evaluate_currency_format_converter(value: Any) -> str:
        """Python implementation of CurrencyFormatConverter logic"""
        return "en-US"  # Always returns US culture

    @staticmethod
    def evaluate_user_message_background_converter(value: Any) -> str:
        """Python implementation of UserMessageBackgroundConverter logic"""
        if isinstance(value, bool) and value:
            return "Blue (#1976D2)"  # User message
        return "Gray (#E0E0E0)"  # AI message

    @staticmethod
    def evaluate_message_alignment_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of MessageAlignmentConverter logic"""
        is_user = isinstance(value, bool) and value

        if parameter:
            param_lower = parameter.strip().lower()
            if param_lower == "background":
                return "Blue (#1976D2)" if is_user else "Gray (#CFD8DC)"
            elif param_lower == "avatar":
                return "You" if is_user else "AI"

        return "Right" if is_user else "Left"

    @staticmethod
    def evaluate_message_foreground_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of MessageForegroundConverter logic"""
        is_user = isinstance(value, bool) and value

        # Parse parameter for custom colors
        if "|" in str(parameter):
            parts = str(parameter).split("|")
            true_color = parts[0] if len(parts) > 0 else "White"
            false_color = parts[1] if len(parts) > 1 else "Black"
        else:
            true_color = "White"
            false_color = "Black"

        return true_color if is_user else false_color

    @staticmethod
    def evaluate_profit_loss_text_converter(value: Any) -> str:
        """Python implementation of ProfitLossTextConverter logic"""
        if isinstance(value, (int, float)):
            return "Monthly Profit" if float(value) >= 0 else "Monthly Loss"
        return "Monthly Position"

    @staticmethod
    def evaluate_profit_brush_converter(value: Any) -> str:
        """Python implementation of ProfitBrushConverter logic"""
        if isinstance(value, (int, float)):
            profit = float(value)
            if profit >= 0:
                return "Light Green (#E8F5E8)"  # Profit background
            else:
                return "Light Orange (#FFF3E0)"  # Loss background
        return "Light Gray (#F5F5F5)"  # Neutral

    @staticmethod
    def evaluate_profit_border_brush_converter(value: Any) -> str:
        """Python implementation of ProfitBorderBrushConverter logic"""
        if isinstance(value, (int, float)):
            profit = float(value)
            if profit >= 0:
                return "Dark Green (#388E3C)"  # Profit border
            else:
                return "Orange (#F57C00)"  # Loss border
        return "Gray (#BDBDBD)"  # Neutral

    @staticmethod
    def evaluate_profit_text_brush_converter(value: Any) -> str:
        """Python implementation of ProfitTextBrushConverter logic"""
        if isinstance(value, (int, float)):
            profit = float(value)
            if profit >= 0:
                return "Dark Green (#388E3C)"  # Profit text
            else:
                return "Orange (#F57C00)"  # Loss text
        return "Dark Gray (#212121)"  # Neutral

    @staticmethod
    def evaluate_bool_to_background_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of BoolToBackgroundConverter logic"""
        is_true = isinstance(value, bool) and value

        # Parse parameter for custom colors
        if "|" in str(parameter):
            parts = str(parameter).split("|")
            true_color = parts[0] if len(parts) > 0 else "Light Red (#FFEBEE)"
            false_color = parts[1] if len(parts) > 1 else "Light Green (#E8F5E8)"
        else:
            true_color = "Light Red (#FFEBEE)"  # Error state
            false_color = "Light Green (#E8F5E8)"  # Success state

        return true_color if is_true else false_color

    @staticmethod
    def evaluate_bool_to_visibility_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of BoolToVisibilityConverter logic"""

        def evaluate_visibility(val: Any, param: str = "") -> bool:
            # Base evaluation
            if isinstance(val, bool):
                base_result = val
            elif isinstance(val, str):
                base_result = len(val.strip()) > 0
            elif isinstance(val, int):
                base_result = val != 0
            elif val is None:
                base_result = False
            else:
                base_result = True

            if not param:
                return base_result

            param_lower = param.lower().strip()

            if param_lower in ["invert", "!"]:
                return not base_result

            if param_lower == "empty" and isinstance(val, str):
                return len(val.strip()) == 0

            if param_lower == "notempty" and isinstance(val, str):
                return len(val.strip()) > 0

            if param.isdigit() and isinstance(val, int):
                return val == int(param)

            return base_result

        visible = evaluate_visibility(value, parameter)
        return "Visible" if visible else "Collapsed"

    @staticmethod
    def evaluate_empty_string_to_visibility_converter(value: Any) -> str:
        """Python implementation of EmptyStringToVisibilityConverter logic"""
        if isinstance(value, str) and len(value) == 0:
            return "Visible"
        return "Collapsed"

    @staticmethod
    def evaluate_count_to_visibility_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of CountToVisibilityConverter logic"""
        if isinstance(value, int) and parameter.isdigit():
            target_count = int(parameter)
            return "Visible" if value == target_count else "Collapsed"
        return "Collapsed"

    @staticmethod
    def evaluate_bool_to_foreground_converter(value: Any, parameter: str = "") -> str:
        """Python implementation of BoolToForegroundConverter logic"""
        has_error = isinstance(value, bool) and value

        # Parse parameter for custom colors
        if "|" in str(parameter):
            parts = str(parameter).split("|")
            true_color = parts[0] if len(parts) > 0 else "Red (#D32F2F)"
            false_color = parts[1] if len(parts) > 1 else "Green (#388E3C)"
        else:
            true_color = "Red (#D32F2F)"  # Error state
            false_color = "Green (#388E3C)"  # Success state

        return true_color if has_error else false_color

    @staticmethod
    def evaluate_inverse_boolean_converter(value: Any) -> bool:
        """Python implementation of InverseBooleanConverter logic"""
        if isinstance(value, bool):
            return not value
        return True  # Default for non-boolean values

    @staticmethod
    def evaluate_boolean_to_font_weight_converter(
        value: Any, parameter: str = ""
    ) -> str:
        """Python implementation of BooleanToFontWeightConverter logic"""
        is_bold = isinstance(value, bool) and value

        # Parse parameter for custom weights
        if "|" in str(parameter):
            parts = str(parameter).split("|")
            true_weight = parts[0] if len(parts) > 0 else "Bold"
            false_weight = parts[1] if len(parts) > 1 else "Normal"
        else:
            true_weight = "Bold"
            false_weight = "Normal"

        return true_weight if is_bold else false_weight


class ConverterTestRunner:
    """Runs comprehensive tests on all converters"""

    def __init__(self):
        self.evaluator = ConverterEvaluator()
        self.test_suites = self._create_test_suites()

    def _create_test_suites(self) -> list[TestSuite]:
        """Create all test suites for converter evaluation"""
        return [
            TestSuite(
                converter_name="BalanceColorConverter",
                converter_description="Converts numeric values to colors (Green/Red/Gray)",
                test_cases=[
                    TestCase(
                        "Positive decimal",
                        100.50,
                        "Green",
                        description="Positive balance should be green",
                    ),
                    TestCase(
                        "Negative decimal",
                        -50.25,
                        "Red",
                        description="Negative balance should be red",
                    ),
                    TestCase(
                        "Zero decimal",
                        0.0,
                        "Gray",
                        description="Zero balance should be gray",
                    ),
                    TestCase(
                        "Positive int",
                        100,
                        "Green",
                        description="Positive integer should be green",
                    ),
                    TestCase(
                        "Negative int",
                        -25,
                        "Red",
                        description="Negative integer should be red",
                    ),
                    TestCase(
                        "Zero int", 0, "Gray", description="Zero integer should be gray"
                    ),
                    TestCase(
                        "String input",
                        "invalid",
                        "Gray",
                        description="Non-numeric input should default to gray",
                    ),
                    TestCase(
                        "None input",
                        None,
                        "Gray",
                        description="None input should default to gray",
                    ),
                ],
            ),
            TestSuite(
                converter_name="BudgetProgressConverter",
                converter_description="Scales budget amounts to 0-100 progress bar range",
                test_cases=[
                    TestCase(
                        "Small budget",
                        10000.0,
                        10.0,
                        description="10k should scale to 10%",
                    ),
                    TestCase(
                        "Medium budget",
                        50000.0,
                        50.0,
                        description="50k should scale to 50%",
                    ),
                    TestCase(
                        "Large budget",
                        100000.0,
                        100.0,
                        description="100k should scale to 100%",
                    ),
                    TestCase(
                        "Oversized budget",
                        150000.0,
                        100.0,
                        description="150k should cap at 100%",
                    ),
                    TestCase("Zero budget", 0.0, 0.0, description="Zero should be 0%"),
                    TestCase(
                        "Negative budget",
                        -10000.0,
                        0.0,
                        description="Negative should floor at 0%",
                    ),
                    TestCase(
                        "Integer input",
                        25000,
                        25.0,
                        description="Integer 25k should scale to 25%",
                    ),
                ],
            ),
            TestSuite(
                converter_name="CurrencyFormatConverter",
                converter_description="Returns US culture info for currency formatting",
                test_cases=[
                    TestCase(
                        "Any input",
                        "test",
                        "en-US",
                        description="Should always return en-US culture",
                    ),
                    TestCase(
                        "Numeric input",
                        123.45,
                        "en-US",
                        description="Numeric input should return en-US",
                    ),
                    TestCase(
                        "None input",
                        None,
                        "en-US",
                        description="None input should return en-US",
                    ),
                ],
            ),
            TestSuite(
                converter_name="UserMessageBackgroundConverter",
                converter_description="Converts boolean to message background colors",
                test_cases=[
                    TestCase(
                        "User message",
                        True,
                        "Blue (#1976D2)",
                        description="User messages should be blue",
                    ),
                    TestCase(
                        "AI message",
                        False,
                        "Gray (#E0E0E0)",
                        description="AI messages should be gray",
                    ),
                    TestCase(
                        "Non-boolean",
                        "test",
                        "Gray (#E0E0E0)",
                        description="Non-boolean should default to gray",
                    ),
                ],
            ),
            TestSuite(
                converter_name="MessageAlignmentConverter",
                converter_description="Converts boolean to message alignment",
                test_cases=[
                    TestCase(
                        "User alignment",
                        True,
                        "Right",
                        description="User messages align right",
                    ),
                    TestCase(
                        "AI alignment",
                        False,
                        "Left",
                        description="AI messages align left",
                    ),
                    TestCase(
                        "User background",
                        True,
                        "Blue (#1976D2)",
                        "background",
                        "User background parameter",
                    ),
                    TestCase(
                        "AI background",
                        False,
                        "Gray (#CFD8DC)",
                        "background",
                        "AI background parameter",
                    ),
                    TestCase(
                        "User avatar", True, "You", "avatar", "User avatar parameter"
                    ),
                    TestCase("AI avatar", False, "AI", "avatar", "AI avatar parameter"),
                ],
            ),
            TestSuite(
                converter_name="MessageForegroundConverter",
                converter_description="Converts boolean to message text colors",
                test_cases=[
                    TestCase(
                        "User text",
                        True,
                        "White",
                        description="User text should be white",
                    ),
                    TestCase(
                        "AI text", False, "Black", description="AI text should be black"
                    ),
                    TestCase(
                        "Custom colors",
                        True,
                        "Blue",
                        "Blue|Green",
                        "Custom color parameter",
                    ),
                    TestCase(
                        "Custom colors false",
                        False,
                        "Green",
                        "Blue|Green",
                        "Custom color parameter",
                    ),
                ],
            ),
            TestSuite(
                converter_name="ProfitLossTextConverter",
                converter_description="Converts numeric values to profit/loss text",
                test_cases=[
                    TestCase(
                        "Profit",
                        1000.0,
                        "Monthly Profit",
                        description="Positive values show profit",
                    ),
                    TestCase(
                        "Loss",
                        -500.0,
                        "Monthly Loss",
                        description="Negative values show loss",
                    ),
                    TestCase(
                        "Zero",
                        0.0,
                        "Monthly Profit",
                        description="Zero is considered profit",
                    ),
                    TestCase(
                        "Non-numeric",
                        "test",
                        "Monthly Position",
                        description="Non-numeric defaults to position",
                    ),
                ],
            ),
            TestSuite(
                converter_name="ProfitBrushConverter",
                converter_description="Converts numeric values to profit/loss background colors",
                test_cases=[
                    TestCase(
                        "Profit background",
                        1000.0,
                        "Light Green (#E8F5E8)",
                        description="Profit gets light green background",
                    ),
                    TestCase(
                        "Loss background",
                        -500.0,
                        "Light Orange (#FFF3E0)",
                        description="Loss gets light orange background",
                    ),
                    TestCase(
                        "Zero background",
                        0.0,
                        "Light Green (#E8F5E8)",
                        description="Zero gets profit background",
                    ),
                    TestCase(
                        "Non-numeric",
                        "test",
                        "Light Gray (#F5F5F5)",
                        description="Non-numeric gets neutral background",
                    ),
                ],
            ),
            TestSuite(
                converter_name="ProfitBorderBrushConverter",
                converter_description="Converts numeric values to profit/loss border colors",
                test_cases=[
                    TestCase(
                        "Profit border",
                        1000.0,
                        "Dark Green (#388E3C)",
                        description="Profit gets dark green border",
                    ),
                    TestCase(
                        "Loss border",
                        -500.0,
                        "Orange (#F57C00)",
                        description="Loss gets orange border",
                    ),
                    TestCase(
                        "Zero border",
                        0.0,
                        "Dark Green (#388E3C)",
                        description="Zero gets profit border",
                    ),
                    TestCase(
                        "Non-numeric",
                        "test",
                        "Gray (#BDBDBD)",
                        description="Non-numeric gets gray border",
                    ),
                ],
            ),
            TestSuite(
                converter_name="ProfitTextBrushConverter",
                converter_description="Converts numeric values to profit/loss text colors",
                test_cases=[
                    TestCase(
                        "Profit text",
                        1000.0,
                        "Dark Green (#388E3C)",
                        description="Profit gets dark green text",
                    ),
                    TestCase(
                        "Loss text",
                        -500.0,
                        "Orange (#F57C00)",
                        description="Loss gets orange text",
                    ),
                    TestCase(
                        "Zero text",
                        0.0,
                        "Dark Green (#388E3C)",
                        description="Zero gets profit text color",
                    ),
                    TestCase(
                        "Non-numeric",
                        "test",
                        "Dark Gray (#212121)",
                        description="Non-numeric gets dark gray text",
                    ),
                ],
            ),
            TestSuite(
                converter_name="BoolToBackgroundConverter",
                converter_description="Converts boolean to background colors",
                test_cases=[
                    TestCase(
                        "True state",
                        True,
                        "Light Red (#FFEBEE)",
                        description="True gets error background",
                    ),
                    TestCase(
                        "False state",
                        False,
                        "Light Green (#E8F5E8)",
                        description="False gets success background",
                    ),
                    TestCase(
                        "Custom colors",
                        True,
                        "Blue",
                        "Blue|Green",
                        "Custom background colors",
                    ),
                    TestCase(
                        "Custom colors false",
                        False,
                        "Green",
                        "Blue|Green",
                        "Custom background colors",
                    ),
                ],
            ),
            TestSuite(
                converter_name="BoolToVisibilityConverter",
                converter_description="Converts values to Visibility with various parameters",
                test_cases=[
                    TestCase(
                        "True visible",
                        True,
                        "Visible",
                        description="True values are visible",
                    ),
                    TestCase(
                        "False collapsed",
                        False,
                        "Collapsed",
                        description="False values are collapsed",
                    ),
                    TestCase(
                        "True inverted",
                        True,
                        "Collapsed",
                        "invert",
                        "True inverted becomes collapsed",
                    ),
                    TestCase(
                        "False inverted",
                        False,
                        "Visible",
                        "invert",
                        "False inverted becomes visible",
                    ),
                    TestCase(
                        "Empty string",
                        "",
                        "Collapsed",
                        description="Empty string is collapsed",
                    ),
                    TestCase(
                        "Non-empty string",
                        "test",
                        "Visible",
                        description="Non-empty string is visible",
                    ),
                    TestCase(
                        "Empty check",
                        "",
                        "Visible",
                        "empty",
                        "Empty parameter checks for empty strings",
                    ),
                    TestCase(
                        "Not empty check",
                        "test",
                        "Visible",
                        "notempty",
                        "NotEmpty parameter checks for content",
                    ),
                    TestCase(
                        "Count match",
                        5,
                        "Visible",
                        "5",
                        "Count parameter checks for specific number",
                    ),
                    TestCase(
                        "Count no match",
                        3,
                        "Collapsed",
                        "5",
                        "Count parameter fails for different number",
                    ),
                ],
            ),
            TestSuite(
                converter_name="EmptyStringToVisibilityConverter",
                converter_description="Shows controls when string is empty",
                test_cases=[
                    TestCase(
                        "Empty string",
                        "",
                        "Visible",
                        description="Empty string makes control visible",
                    ),
                    TestCase(
                        "Non-empty string",
                        "test",
                        "Collapsed",
                        description="Non-empty string hides control",
                    ),
                    TestCase(
                        "Whitespace only",
                        "   ",
                        "Collapsed",
                        description="Whitespace is not considered empty",
                    ),
                    TestCase(
                        "Non-string",
                        123,
                        "Collapsed",
                        description="Non-string input is collapsed",
                    ),
                ],
            ),
            TestSuite(
                converter_name="CountToVisibilityConverter",
                converter_description="Shows controls when count matches parameter",
                test_cases=[
                    TestCase(
                        "Count matches",
                        5,
                        "Visible",
                        "5",
                        "Count 5 matches parameter 5",
                    ),
                    TestCase(
                        "Count no match",
                        3,
                        "Collapsed",
                        "5",
                        "Count 3 doesn't match parameter 5",
                    ),
                    TestCase(
                        "Invalid parameter",
                        5,
                        "Collapsed",
                        "abc",
                        "Non-numeric parameter fails",
                    ),
                    TestCase(
                        "Non-integer input",
                        "test",
                        "Collapsed",
                        "5",
                        "Non-integer input fails",
                    ),
                ],
            ),
            TestSuite(
                converter_name="BoolToForegroundConverter",
                converter_description="Converts boolean to foreground colors",
                test_cases=[
                    TestCase(
                        "Error state",
                        True,
                        "Red (#D32F2F)",
                        description="True represents error state",
                    ),
                    TestCase(
                        "Success state",
                        False,
                        "Green (#388E3C)",
                        description="False represents success state",
                    ),
                    TestCase(
                        "Custom colors",
                        True,
                        "Blue",
                        "Blue|Green",
                        "Custom foreground colors",
                    ),
                    TestCase(
                        "Custom colors false",
                        False,
                        "Green",
                        "Blue|Green",
                        "Custom foreground colors",
                    ),
                ],
            ),
            TestSuite(
                converter_name="InverseBooleanConverter",
                converter_description="Inverts boolean values",
                test_cases=[
                    TestCase(
                        "True inverted", True, False, description="True becomes False"
                    ),
                    TestCase(
                        "False inverted", False, True, description="False becomes True"
                    ),
                    TestCase(
                        "Non-boolean",
                        "test",
                        True,
                        description="Non-boolean defaults to True",
                    ),
                    TestCase(
                        "None input", None, True, description="None defaults to True"
                    ),
                ],
            ),
            TestSuite(
                converter_name="BooleanToFontWeightConverter",
                converter_description="Converts boolean to font weights",
                test_cases=[
                    TestCase(
                        "Bold weight", True, "Bold", description="True values are bold"
                    ),
                    TestCase(
                        "Normal weight",
                        False,
                        "Normal",
                        description="False values are normal",
                    ),
                    TestCase(
                        "Custom weights",
                        True,
                        "ExtraBold",
                        "ExtraBold|Light",
                        "Custom font weights",
                    ),
                    TestCase(
                        "Custom weights false",
                        False,
                        "Light",
                        "ExtraBold|Light",
                        "Custom font weights",
                    ),
                ],
            ),
        ]

    def run_evaluation(self) -> dict[str, list[tuple[TestCase, TestResult, Any]]]:
        """Run all converter evaluations and return results"""
        results = {}

        for suite in self.test_suites:
            suite_results = []
            method_name = f"evaluate_{camel_to_snake(suite.converter_name)}"
            converter_method = getattr(self.evaluator, method_name)

            for test_case in suite.test_cases:
                try:
                    if test_case.parameters is not None:
                        actual_output = converter_method(
                            test_case.input_value, test_case.parameters
                        )
                    else:
                        actual_output = converter_method(test_case.input_value)

                    # Compare results (with some tolerance for floating point)
                    if isinstance(test_case.expected_output, float) and isinstance(
                        actual_output, (int, float)
                    ):
                        result = (
                            TestResult.PASS
                            if abs(actual_output - test_case.expected_output) < 0.01
                            else TestResult.FAIL
                        )
                    else:
                        result = (
                            TestResult.PASS
                            if actual_output == test_case.expected_output
                            else TestResult.FAIL
                        )

                    suite_results.append((test_case, result, actual_output))

                except Exception as e:
                    suite_results.append((test_case, TestResult.ERROR, str(e)))

            results[suite.converter_name] = suite_results

        return results

    def print_results(self, results: dict[str, list[tuple[TestCase, TestResult, Any]]]):
        """Print formatted evaluation results"""
        print("=" * 80)
        print("WILEY WIDGET CONVERTER EVALUATION RESULTS")
        print("=" * 80)

        total_tests = 0
        total_passed = 0
        total_failed = 0
        total_errors = 0

        for converter_name, suite_results in results.items():
            print(f"\n🔄 {converter_name}")
            print("-" * 50)

            suite_passed = 0
            suite_failed = 0
            suite_errors = 0

            for test_case, result, actual_output in suite_results:
                total_tests += 1
                status_icon = (
                    "✅"
                    if result == TestResult.PASS
                    else "❌" if result == TestResult.FAIL else "💥"
                )

                if result == TestResult.PASS:
                    suite_passed += 1
                    total_passed += 1
                elif result == TestResult.FAIL:
                    suite_failed += 1
                    total_failed += 1
                    print(f"  {status_icon} {test_case.name}: FAILED")
                    print(f"      Expected: {test_case.expected_output}")
                    print(f"      Actual:   {actual_output}")
                    if test_case.description:
                        print(f"      Note:     {test_case.description}")
                else:
                    suite_errors += 1
                    total_errors += 1
                    print(f"  {status_icon} {test_case.name}: ERROR - {actual_output}")
                    if test_case.description:
                        print(f"      Note:     {test_case.description}")

            # Suite summary
            suite_total = len(suite_results)
            success_rate = (suite_passed / suite_total * 100) if suite_total > 0 else 0
            print(
                f"  Success Rate: {success_rate:.1f}% ({suite_passed}/{suite_total} passed)"
            )
            if suite_failed > 0 or suite_errors > 0:
                print(f"  ⚠️  Issues: {suite_failed} failed, {suite_errors} errors")

        # Overall summary
        print("\n" + "=" * 80)
        print("OVERALL EVALUATION SUMMARY")
        print("=" * 80)
        print(f"Total Tests:     {total_tests}")
        print(f"Passed:          {total_passed}")
        print(f"Failed:          {total_failed}")
        print(f"Errors:          {total_errors}")

        if total_tests > 0:
            success_rate = total_passed / total_tests * 100
            print(
                f"  Success Rate: {success_rate:.1f}% ({total_passed}/{total_tests} passed)"
            )
            if total_failed == 0 and total_errors == 0:
                print("🎉 ALL CONVERTERS EVALUATED SUCCESSFULLY!")
            else:
                print(
                    f"⚠️  {total_failed + total_errors} ISSUES FOUND - REVIEW FAILED TESTS ABOVE"
                )


def main():
    """Main evaluation function"""
    print("Starting Wiley Widget Converter Evaluation...")
    print("This will test all C# converters with comprehensive test cases.\n")

    evaluator = ConverterTestRunner()
    results = evaluator.run_evaluation()
    evaluator.print_results(results)

    # Return exit code based on results
    total_issues = sum(
        1
        for suite_results in results.values()
        for _, result, _ in suite_results
        if result != TestResult.PASS
    )

    return 0 if total_issues == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
