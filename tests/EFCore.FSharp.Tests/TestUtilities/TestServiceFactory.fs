namespace EntityFrameworkCore.FSharp.Test.TestUtilities

open System
open System.Collections.Concurrent
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata.Internal
open System.Linq
open Microsoft.EntityFrameworkCore.ChangeTracking.Internal
open Microsoft.EntityFrameworkCore.Metadata

type Implementation = {
    Type : Type
    Implementation : obj }

type ImplementationType = {
    Type : Type
    ImplementationType : Type }

module TestServiceFactory =
    open Microsoft.Extensions.DependencyInjection
    open System.Collections.Generic

    let private factories = ConcurrentDictionary<Type, IServiceProvider>()

    let private wellKnownExceptions =
        [
            {
                Type = typeof<IRegisteredServices>
                Implementation = new RegisteredServices(Enumerable.Empty<Type>()) :> obj}
            {
                Type = typeof<ServiceParameterBindingFactory>
                Implementation = new ServiceParameterBindingFactory(typeof<IStateManager>) :> obj}
        ]

    let private tryGetEnumerableType (t : Type) =
        
        let typeInfo = System.Reflection.IntrospectionExtensions.GetTypeInfo t
        
        if not (typeInfo.IsGenericTypeDefinition)
               && typeInfo.IsGenericType
               && t.GetGenericTypeDefinition() = typeof<IEnumerable<_>> then
                    typeInfo.GenericTypeArguments.[0] |> Some
                else
                    None

    let private getImplementationTypes (serviceType : Type) =

        if serviceType.IsInterface then
            let serviceTypeOpt = tryGetEnumerableType serviceType

            let st = match serviceTypeOpt with | Some s -> s | None -> serviceType
            let implementationTypes =
                st
                    .Assembly
                    .GetTypes()
                    |> Seq.filter (fun t -> st.IsAssignableFrom(t) && (not t.IsAbstract))

            match serviceTypeOpt with
            | None ->
                if implementationTypes |> Seq.length = 1 then
                    let msg = sprintf "Cannot use 'TestServiceFactory' for '%s': no single implementation type in same assembly." (serviceType.DisplayName())
                    invalidOp msg

                [ { Type = serviceType; ImplementationType = implementationTypes |> Seq.head }]
            | Some s ->
                implementationTypes
                    |> Seq.map(fun t -> { Type = s; ImplementationType = t })
                    |> Seq.toList
        else
            [ { Type = serviceType; ImplementationType = serviceType } ]            
        
    let rec private addType (serviceCollection :ServiceCollection) (serviceType: Type) (specialCases : Implementation list) =
        let implementation =
            specialCases
            |> Seq.filter (fun s -> s.Type = serviceType)
            |> Seq.map (fun s -> s.Implementation)
            |> Seq.tryHead

        match implementation with
        | Some i -> serviceCollection.AddSingleton(serviceType, i) |> ignore
        | None ->
            let types = getImplementationTypes serviceType
            types
                |> Seq.iter(fun t ->
                    let implementation =
                        specialCases
                        |> Seq.filter (fun s -> s.Type = serviceType)
                        |> Seq.map (fun s -> s.Implementation)
                        |> Seq.tryHead

                    match implementation with
                    | Some i -> serviceCollection.AddSingleton(serviceType, i) |> ignore
                    | None ->
                        serviceCollection.AddSingleton(t.Type, t.ImplementationType) |> ignore

                        let constructors = t.ImplementationType.GetConstructors()
                        let maxParamLength = constructors |> Seq.map(fun c -> c.GetParameters().Length) |> Seq.max

                        let constructor =
                            constructors
                            |> Seq.filter (fun c -> (c.GetParameters().Length) = maxParamLength)
                            |> Seq.tryHead

                        match constructor with
                        | None ->
                            let msg = sprintf "Cannot use 'TestServiceFactory' for '%s': no public constructor." (t.ImplementationType.DisplayName())
                            invalidOp msg
                        | Some c ->
                                c.GetParameters()
                                    |> Seq.iter(fun p -> addType serviceCollection p.ParameterType specialCases |> ignore)
                )

        serviceCollection

    let create<'a when 'a : not struct> (specialCases : Implementation list) =

        let exceptions = specialCases @ wellKnownExceptions

        let serviceprovider =
            (addType (ServiceCollection()) typeof<'a> exceptions)
            |> ServiceCollectionContainerBuilderExtensions.BuildServiceProvider
            :> IServiceProvider

        factories.GetOrAdd(typeof<'a>, fun t -> serviceprovider).GetService<'a>()



    