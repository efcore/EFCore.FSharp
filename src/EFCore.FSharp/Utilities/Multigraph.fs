namespace EntityFrameworkCore.FSharp.Utilities

open System.Collections.Generic

type Multigraph<'TVertex, 'TEdge when 'TVertex: equality>() =
    let vertexSet = HashSet<'TVertex>()

    let successorMap =
        Dictionary<'TVertex, Dictionary<'TVertex, List<'TEdge>>>()

    let predecessorMap =
        Dictionary<'TVertex, HashSet<'TVertex>>()

    member this.AddVertices(vertices: 'TVertex seq) = vertexSet.UnionWith(vertices)

    member this.AddEdge(from: 'TVertex, to': 'TVertex, edge: 'TEdge) =
        let successorEdges =
            match successorMap.TryGetValue from with
            | true, successorEdges -> successorEdges
            | _ ->
                let successorEdges = Dictionary<'TVertex, List<'TEdge>>()
                successorMap.Add(from, successorEdges)
                successorEdges

        let edgeList =
            match successorEdges.TryGetValue to' with
            | true, edgeList -> edgeList
            | _ ->
                let edgeList = List()
                successorEdges.Add(to', edgeList)
                edgeList

        edgeList.Add(edge)

        let predecessors =
            match predecessorMap.TryGetValue to' with
            | true, predecessors -> predecessors
            | _ ->
                let predecessors = HashSet()
                predecessorMap.Add(to', predecessors)
                predecessors

        predecessors.Add(from) |> ignore

    member private this.ThrowCycle(cycle: List<'TVertex>) =
        let cycleString =
            cycle
            |> Seq.map string
            |> Seq.fold (fun c n -> $"{c} -> {n}") ""

        invalidOp $"Circular dependency {cycleString}"

    member this.TopologicalSort() : 'TVertex seq =
        let sortedQueue = List()
        let predecessorCounts = Dictionary<_, _>()

        let getOutgoingNeighbour (from: 'TVertex) =
            match successorMap.TryGetValue from with
            | true, successorSet -> seq { yield! successorSet.Keys }
            | _ -> Seq.empty

        let getIncomingNeighbours to' =
            match predecessorMap.TryGetValue to' with
            | true, predecessors -> seq { yield! predecessors }
            | _ -> Seq.empty

        vertexSet
        |> Seq.iter
            (fun v ->
                getOutgoingNeighbour v
                |> Seq.iter
                    (fun n ->
                        if predecessorCounts.ContainsKey(n) then
                            predecessorCounts.[n] <- predecessorCounts.[n] + 1
                        else
                            predecessorCounts.[n] <- 1))

        vertexSet
        |> Seq.filter (predecessorCounts.ContainsKey >> not)
        |> sortedQueue.AddRange

        let mutable index = 0

        while sortedQueue.Count < vertexSet.Count do
            while index < sortedQueue.Count do
                getOutgoingNeighbour (sortedQueue.[index])
                |> Seq.filter predecessorCounts.ContainsKey
                |> Seq.iter
                    (fun n ->
                        predecessorCounts.[n] <- predecessorCounts.[n] - 1

                        if predecessorCounts.[n] = 0 then
                            sortedQueue.Add(n)
                            predecessorCounts.Remove(n) |> ignore)

                index <- index + 1

            if sortedQueue.Capacity < vertexSet.Count then
                let mutable currentCycleVertex =
                    vertexSet
                    |> Seq.find predecessorCounts.ContainsKey

                let cycle = [ currentCycleVertex ] |> ResizeArray
                let mutable finished = false

                let rec loop vertices =
                    match vertices with
                    | v :: rest ->
                        if predecessorCounts.[v] <> 0 then
                            predecessorCounts.[currentCycleVertex] <- predecessorCounts.[currentCycleVertex] - 1
                            currentCycleVertex <- v
                            cycle.Add currentCycleVertex
                            finished <- predecessorCounts.[v] = -1
                        else
                            loop rest
                    | _ -> ()

                while not finished do
                    getIncomingNeighbours currentCycleVertex
                    |> Seq.filter predecessorCounts.ContainsKey
                    |> Seq.toList
                    |> loop

                cycle.Reverse()
                this.ThrowCycle(cycle)

        seq { yield! sortedQueue }
