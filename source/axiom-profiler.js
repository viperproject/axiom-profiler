var graph = {
    data: null,
    backup: null,
    network: null,
    defaultColor: { border: "#2B7CE9",
                    background: "#97C2FC",
                    highlight: { border: "#41A906", background: "#7BE141" } },

    resize: function() {
        var height = $(window).height() - $("#control-panel").outerHeight(true);
        $("#network").height(height);
    },

    draw: function(nodes, edges) {
        $.each(edges, function(id, it) {
            it.title = it.value + " instantiations";
        });

        if (graph.backup === null) {
            graph.backup = { nodes: new vis.DataSet(nodes), edges: new vis.DataSet(edges) };
        }

        graph.data = { nodes: new vis.DataSet(nodes), edges: new vis.DataSet(edges) };

        var options = {
            nodes: {
                shape: 'dot',
                scaling: { label: { min: 12, max: 30 } },
                font: { face: "Consolas, monospace" },
                color: graph.defaultColor
            }, layout: {
                randomSeed: 0
            }, edges: {
                smooth: { type: "continuous" },
                // color: { color: "#2B7CE9", highlight: "#41A906" },
                color: {inherit: 'both'}, // NOTE Set this to 'from' for performance
                arrows: { to: { scaleFactor: 0.5 } }
            }, physics: {
                stabilization: false,
                barnesHut: { gravitationalConstant: -20000, springConstant: 0.01, centralGravity: 1 }
            }, interaction: {
                multiselect: true,
                hideEdgesOnDrag: true
            }
        };

        var container = document.getElementById('network');
        graph.network = new vis.Network(container, graph.data, options);
        graph.network.on("click", graph.updateEditor);
        graph.setupSliders();
    },

    percentile: function(array, fraction) {
        array.sort(function(a, b) { return a - b; });
        return array[Math.floor(array.length * fraction)];
    },

    setupSlider: function(id, array, percentile, filterKey) {
        var values = $.map(array, function(it, id) { return it.value; });
        values.push(0);

        var max = Math.max.apply(Math, values);
        var value = graph.percentile(values, percentile);

        $(id).slider({ min: 0,
                       max: max,
                       value: value,
                       range: "min",
                       animate: "slow",
                       slide: function(event, ui) { graph.applyFilter({ filterKey: ui.value }); },
                       change: function(event, ui) { graph.applyFilter({ filterKey: ui.value }); } });
    },

    setupSliders: function(percentile) {
        percentile = percentile === undefined ? 0.9 : percentile;
        graph.setupSlider("#node-slider", graph.data.nodes.get(), percentile, "minCost");
        graph.setupSlider("#edge-slider", graph.data.edges.get(), percentile, "minCount");
        graph.applyFilter();
    },

    updateEditor: function(obj) {
        var selected = graph.network.getSelectedNodes();
        var editor = $("#node-name");
        if (selected.length !== 1) {
            editor.hide(400);
        } else {
            var node = graph.data.nodes.get(selected[0]);
            editor.val(node.label);
            editor.show(400);
        }
        graph.applyFilter();
    },

    init: function(nodes, edges) {
        $(window).bind("resize", graph.resize);

        $("#hide-orphan").button().click(function(event) { graph.applyFilter(); });
        $("#trim-hidden").button().click(function(event) { graph.trimHidden(); event.preventDefault(); });
        $("#reset").button().click(function(event) { graph.reset(); event.preventDefault(); });
        $("#text-filter").on("input", function() { graph.applyFilter(); });
        $("#node-name").on("input", function() { graph.renameSelected(); });
        $("body").keydown(function(event) { if (event.keyCode === 46) { graph.removeSelected(); } });

        graph.resize();
        graph.draw(nodes, edges);
    },

    nodeMatchesSearch: function(search, node) {
        search = search.trim();
        if (search === "") {
            return false;
        }

        var title = node.title.toLowerCase();
        var body = node.search.toLowerCase();
        var kwds = (search || "").toLowerCase().split(",");

        return kwds.every(function(kwd) {
            kwd = kwd.trim();
            return ~title.indexOf(kwd) || ~body.indexOf(kwd);
        });
    },

    isOrphan: function(id) {
        return graph.network.getConnectedEdges(id).every(function(id) {
            return graph.data.edges.get(id).hidden;
        });
    },

    getColor: function(node, isGrayedOut, search) {
        if (isGrayedOut) {
            return 'rgb(200,200,200)';
        } else if (node.label !== node.id) {
            return { border: "#C37F00", // orange
                     background: "#FFA807",
                     highlight: { border: "#C37F00", background: "#FFCA66" } };
        } else if (graph.nodeMatchesSearch(search, node)) {
            return { border: "#7C29F0",
                     background: "#AD85E4",
                     highlight: { border: "#7C29F0", background: "#D3BDF0" } };
        } else {
            return graph.defaultColor;
        }
    },

    computeGrayOutMap: function() {
        var grayedOut = [];
        var selected = graph.network.getSelectedNodes();
        var hasSelection = (selected.length > 0);

        graph.data.nodes.forEach(function(node) {
            grayedOut[node.id] = hasSelection;
        });

        $.each(selected, function(_, nodeid) {
            grayedOut[nodeid] = false;
            $.each(graph.network.getConnectedNodes(nodeid), function(_, neighbourid) {
                grayedOut[neighbourid] = false;
            });
        });

        return grayedOut;
    },

    applyFilter: function(filter) {
        var nodes = graph.data.nodes;
        var edges = graph.data.edges;

        var defaults = {
            minCost: $("#node-slider").slider("value"),
            minCount: $("#edge-slider").slider("value"),
            contains: $("#text-filter").val(),
            hideOrphan: $("#hide-orphan").is(':checked')
        };

        filter = $.extend({}, defaults, filter);

        var grayedOut = graph.computeGrayOutMap();

        nodes.update(nodes.map(function(node) {
            return { id: node.id,
                     hidden: node.value < filter.minCost,
                     shape: node.mergedCount > 1 ? "square" : "dot",
                     color: graph.getColor(node, grayedOut[node.id], filter.contains) };
        }));

        edges.update(edges.map(function(edge) {
            return { id: edge.id,
                     hidden: (edge.value < filter.minCount ||
                              nodes.get(edge.from).hidden ||
                              nodes.get(edge.to).hidden) };
        }));

        if (filter.hideOrphan) {
            nodes.update(nodes.map(function(node) {
                return { id: node.id,
                         hidden: node.hidden || graph.isOrphan(node.id) };
            }));
        }
    },

    renameSelected: function() {
        var updated = $.map(graph.network.getSelectedNodes(), function(id) {
            var label = $("#node-name").val();
            return { id: id,
                     label: label !== "" ? label : id };
        });
        graph.data.nodes.update(updated);
        graph.backup.nodes.update(updated);
        graph.applyFilter();
    },

    removeSelected: function() {
        graph.data.nodes.remove(graph.network.getSelectedNodes());
        graph.data.edges.remove(graph.network.getSelectedEdges());
        graph.updateEditor();
        graph.applyFilter(); // Trim orphans
    },

    reset: function() {
        graph.data.nodes.update(graph.backup.nodes.get());
        graph.data.edges.update(graph.backup.edges.get());
        graph.setupSliders();
    },

    removeWithPredicate: function(dataset, predicate) {
        dataset.remove(dataset.get({ filter: predicate }));
    },

    trimHidden: function() {
        var isHidden = function(item) { return item.hidden; };
        graph.removeWithPredicate(graph.data.nodes, isHidden);
        graph.removeWithPredicate(graph.data.edges, isHidden);
        graph.setupSliders(0);
    }
};
