# Example pytest template if you have Python-based converter logic or tooling to test
def test_converter_transforms_value():
    # Arrange
    input_value = 123.45

    # Act
    result = str(input_value)

    # Assert
    assert "123" in result
