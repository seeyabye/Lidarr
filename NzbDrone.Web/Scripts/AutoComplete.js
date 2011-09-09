﻿jQuery(document).ready(function () {
    $.ajaxSetup({
        cache: false
    });

    bindFolderAutoComplete(".folderLookup");
    bindSeriesAutoComplete(".seriesLookup");
});

function bindFolderAutoComplete(selector) {
    
    $(selector).each(function (index, element) {
        $(element).autocomplete({
            //source: "/Directory/GetDirectories",
            source: function (request, response) {
                $.ajax({
                    url: "/Directory/GetDirectories",
                    dataType: "json",
                    data: {
                        term: request.term
                    },
                    success: function (data) {
                        var re = $.ui.autocomplete.escapeRegex(request.term);
                        var matcher = new RegExp("^" + re, "i");
                        response($.grep(data, function (item) { return matcher.test(item); }));
                    }
                });
            },
            minLength: 3
        });
    });
}

function bindSeriesAutoComplete(selector) {

    $(selector).each(function (index, element) {
        $(element).autocomplete({
            source: "/AddSeries/LookupSeries",
            minLength: 3,
            delay: 500,
            select: function (event, ui) {
                $(this).val(ui.item.Title);
                return false;
            }
        })
	    .data("autocomplete")._renderItem = function (ul, item) {
	        return $("<li></li>")
			.data("item.autocomplete", item)
			.append("<a><strong>" + item.Title + "</strong><br>" + item.FirstAired + "</a>")
			.appendTo(ul);
	    };
    });
}