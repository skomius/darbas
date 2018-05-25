<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<MetersIndexViewModel>" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	<%= Html.Encode(DarsResources.Meters_Title) %>
</asp:Content>

<asp:Content ID="Content4" ContentPlaceHolderId="HeadContent" runat="server">

    <link href="<%= Url.Css("ui.jqgrid.css") %>" rel="stylesheet" type="text/css" />
    <script type="text/javascript" src="<%= Url.GridLocaleScript() %>"></script>
    <script type="text/javascript" src="<%= Url.Script("jquery.jqGrid.min.js") %>"></script>
    <script type="text/javascript" src="<%= Url.Script("jquery.dars.js") %>"></script>

    <script type="text/javascript">

        $(function () {

            $.DarsJqGrid("#metersList", {
                url: '<%= Url.Action("DynamicMeterGridData", "Meters") %>',
                colNames: ['id', '<%= Html.Encode(DarsResources._Name) %>',
                            '<%= Html.Encode(DarsResources._Type) %>',
                           '<%= Html.Encode(DarsResources._Number) %>',
                           '<%= Html.Encode(DarsResources._Connection) %>',
                           '<%= Html.Encode(DarsResources.DataPointEdit_AssignedToGroup) %>',
                           '<%= Html.Encode(DarsResources._DataPoint) %>',
                           '<%= Html.Encode(DarsResources.FWVersion) %>',
                           '<%= Html.Encode(DarsResources.MeterType) %>',
                           '<%= Html.Encode(DarsResources.Modification) %>',
                           '<%= Html.Encode(DarsResources.Registered) %>',
                           '<%= Html.Encode(DarsResources.ParameterSet) %>'], 
                colModel: [
                          { name: "id", index: 'id', width: 1, hidden: true, key: true },
                          { name: 'Name', index: 'Name', width: 80, align: 'left', hidden: false },
                          { name: 'MeterType', index: 'Configuration', width: 40, align: 'left', search: true, sortable: false },
                          { name: 'Number', index: 'Number', width: 40, align: 'left', sortable: false },
                          { name: 'Controller', index: 'MainConnection.ConnString', width: 80, align: 'left', true: false, search: true },
                          { name: 'Group', index: 'GroupList', width: 80, align: 'left', sortable: false, search: true },
                          { name: 'DataPoint.Name', index: 'DataPoint.Name', width: 80, align: 'left', hidden: <%= (!WebSettings.MeterDatapoint).ToString().ToLower()%>, sortable: false, search: false },
                          { name: 'IntFwVersion', index: 'IntFwVersion', width: 80, align: 'left', sortable: false, search: true },
                          { name: 'IntType', index: 'IntType', width: 80, align: 'left', sortable: false, search: true },
                          { name: 'IntModification', index: 'IntModification', width: 80, align: 'left', sortable: false, search: true },
                          { name: 'Registered', index: 'Registered', width: 60, align: 'left', sortable: false, search: true },
                          { name: 'ParameterSet.Name', index: 'ParameterSet.Name', width: 80, align: 'left', sortable: false, search: true },
                ],
                ondblClickRow: function (id) {
                    setUrl('Device/' + id);
                },
                serializeGridData: function (postData) {
                    var currentFilters = postData.filters;
                    if (currentFilters === undefined) {
                        currentFilters = "{ \"groupOp\": \"AND\", \"rules\": [] }";
                    }

                    var searchjson = JSON.parse(currentFilters);

                    for (var i = searchjson.rules.length - 1; i >= 0; i--) {
                        if (searchjson.rules[i].field === "Parameters") {
                            searchjson.rules.splice(i, 1);
                        }
                    }

                    var value = $("#mainFilter").val();

                    if (value.length > 1) {
                        searchjson.rules.push({ field: "Parameters", op: "eq", data: value });
                    }

                    postData.filters = JSON.stringify(searchjson);
                    return postData;
                },

            }, {
                editlink: function (id) { return '<%= Url.Action("Edit") %>/' + id; },
                checklink: function (id) { return 'Meters/#/AddControllerMeter/' + id; },
                dellink: function (id) { return '<%= Url.Action("Delete") %>/' + id; },
                edit: '<%= DarsResources._Edit %>',
                check: 'asdfsadf',
                del: '<%= DarsResources._Delete %>'
            });

            $("#metersList").jqGrid('filterToolbar', { stringResult: true, searchOnEnter: true });


            $('#mainFilter').bind("enterKey", function (e) {
                $("#metersList").trigger('reloadGrid');
            });

            $('#mainFilter').keyup(function (e) {
                if (e.keyCode == 13) {
                    $(this).trigger("enterKey");
                }
            });
        });
        
    </script>

    <script type="text/javascript">
        $(document).ready(function () {

            $("#wnd_exceeded").dialog({
                autoOpen: false,
                height: 'auto',
                width: 350,
                modal: true,
                resizable: false,
                buttons: {
                    Ok: function () {
                        $(this).dialog("close");
                    }
                },
                close: {}
            });

        });

        function ExceededClick() {
            $("#wnd_exceeded").dialog("open");
        }
    </script>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <% Html.RenderPartial("ResultMessage"); %>
    
    <div>

        <div class="action-panel">

            <% if(Model.LicenseIsValid) { %>
                <%= Html.ActionLink(DarsResources.Meters_RegisterMeter, "Create", null, new { @class = "btn btn-small", id = "btnRegisterMeter"})%>
            
            <% } else { %>
                  <a href="#" class="btn btn-small" onclick="ExceededClick(); return false;">
                    <%=DarsResources.Meters_RegisterMeter %>
                </a>
            <% } %>

            <%= Html.ActionLink(DarsResources.Meters_BatchSetPassword, "BatchSetPassword", null, new { @class = "btn btn-small"}) %>

             <%--  <a href="Meters/#/AddControllerMeter/0">
                <%= DarsResources.DevicesIndex_RegisterMeterController %>
                </a>--%>

        </div>
    
        <div class="infoContentDiv">
            <input type="text" id="mainFilter" style="width:100%" placeholder="tariff.name = LV-4T & device.integration_period != 60"></input>
            <table id="metersList" class="scroll" cellpadding="0" cellspacing="0" style="width:100%"></table>
        </div>

        <div ng-view />

    </div>
	
    <div class="mainTop" style="display:none; background-color:#fff;" id="infoContent">
    </div>

    <div id="wnd_exceeded" style="display: none; title="<%= DarsResources.License %>" >
        <div class="exceeded"><%= DarsResources.License_ExceedMeterCount %></div>
        <div class="exceededInfoLink">
            <%= Html.ActionLink<Dars.Web.Controllers.LicenseController>(a => a.LicenseInfo(), DarsResources.License_LicenseInformation) %>
        </div>
    </div>

</asp:Content>
