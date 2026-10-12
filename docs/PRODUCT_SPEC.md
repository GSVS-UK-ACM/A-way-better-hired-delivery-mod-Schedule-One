# Vehicle Handlers Product Specification

Status: Approved for implementation  
Target: Schedule I 0.4.7f12 IL2CPP

## Outcome

Add a distinct hireable Handler/Driver employee that moves an existing player-owned land vehicle to a selected loading bay while respecting normal delivery occupancy and reservation rules.

## Employee behavior

- Handler is a distinct player-facing employee type using `EEmployeeType.Handler`.
- Hiring, wages, beds, lockers, saves, firing, and leave/despawn use base employee systems.
- Each Handler owns exactly one vehicle assignment and one destination bay assignment.
- The Handler cannot work when unpaid, unassigned, fired, or missing a valid vehicle or destination.

## Configuration

The management clipboard provides:

- Owned vehicle selection.
- Destination property selection.
- Destination loading-bay selection.
- Assignment validation.
- Load into assigned bay.
- Hide/release bay.
- Reconfiguration while no trip is active.

Eligible destinations are owned Barn, Bungalow, Storage Unit, Dock Warehouse, and Mansion properties. A vehicle may begin anywhere it is safely parked or positioned on owned land.

## Vehicle invariants

- The assigned `LandVehicle` GUID identifies the vehicle.
- The mod never creates a replacement vehicle for a valid assignment.
- Ownership, contents, color, GUID, and save identity survive every move.
- Occupied, destroyed, delivery-controlled, or otherwise unsafe vehicles are rejected.
- Source loading bays are released when the assigned vehicle leaves.

## Bay invariants

- A Handler reserves its destination before removing the vehicle from its source state.
- Handler reservations make bays unavailable to standard deliveries.
- Standard-delivery occupancy/reservations block Handler placement.
- Physical loading-dock occupancy is authoritative after placement.
- Reservations are released on completion, failure, firing, deletion, despawn, configuration removal, and mod unload.

## Travel model

Version 1 uses a short game-time trip and physical teleportation rather than road pathfinding. The same physical vehicle is hidden or held in a safe non-bay state during transit, then aligned to the destination dock when it becomes available.

