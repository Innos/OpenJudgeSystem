﻿$(document).ready(function () {

    $.validator.addMethod(
        'date',
        function (value, element, params) {
            if (this.optional(element)) {
                return true;
            };
            var result = false;
            try {
                var date = kendo.parseDate(value, "dd/MM/yyyy HH:mm:ss");
                result = true;
                if (!date) {
                    result = false;
                }
            } catch (err) {
                result = false;
            }
            return result;
        },
        ''
    );

    $.validator.setDefaults({ ignore: '' });

    var input = $("#SourceCodeSizeLimit");
    var numericTextBox = input.data("kendoNumericTextBox");
    var checkbox = $('#enable-sclimit');

    if (numericTextBox.value() != null && numericTextBox.value() != 0)
    {
        checkbox.attr('checked', true);
        numericTextBox.enable(true);
        input.attr("data-val-required", "Лимита е задължителен!");

        $("form").removeData("validator");
        $("form").removeData("unobtrusiveValidation");
        $.validator.unobtrusive.parse($('form'));
    }

    checkbox.change(function () {

        if ($(this).is(':checked')) {
            numericTextBox.enable(true);
            input.attr("data-val-required", "Лимита е задължителен!");

            $("form").removeData("validator");
            $("form").removeData("unobtrusiveValidation");
            $.validator.unobtrusive.parse($('form'));
        }
        else {
            numericTextBox.enable(false);
            input.removeAttr("data-val-required");

            $("form").removeData("validator");
            $("form").removeData("unobtrusiveValidation");
            $.validator.unobtrusive.parse($('form'));
        }
    });

    $('#checkers-tooltip').kendoTooltip({
        content: kendo.template($("#checkers-template").html()),
        width: 580,
        position: "bottom"
    });
});