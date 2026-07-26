import { Component, property, script } from '@axrone/ecs-runtime';
import { HydrationMarker } from './HydrationMarker';

@script({
	scriptName: 'ComponentRefFollower',
})
export class ComponentRefFollower extends Component {
	@property({ type: HydrationMarker })
	public marker: HydrationMarker | null = null;
}
